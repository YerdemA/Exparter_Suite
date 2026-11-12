"""Seeds a clean local database with fictional people for the store screenshots.

Run against a local API on an empty database (docs/store/listing.md, section 3) so that
neither test users nor real ones appear in the shots. Sign in as elif_ornek afterwards.

Sessions that have already taken place are planned for a later day and then moved into
the past in the database, so the script also needs the local PostgreSQL container.
"""
import datetime, json, os, re, subprocess, sys, time, urllib.parse, urllib.request, urllib.error, uuid

API = "http://localhost:5244/api/v1"
MAILPIT = "http://localhost:8025/api/v1"
PASSWORD = "Ornek-Parola-2026"
POSTGRES_CONTAINER = os.environ.get("POSTGRES_CONTAINER", "exparter-postgres-1")
DATABASE = os.environ.get("SCREENSHOT_DATABASE", "exparter_screens")
NOW = datetime.datetime.now(datetime.timezone.utc)


def call(method, path, body=None, token=None, base=API):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        sys.exit(f"{method} {path} -> {e.code} {e.read().decode()}")


skills = {}
for page in range(1, 10):
    res = call("GET", f"/skills?page={page}&pageSize=50")
    for s in res["items"]:
        skills[s["name"]] = s["id"]
    if len(res["items"]) < 50:
        break

PEOPLE = {
    "elif": ("Elif Yıldız", "34",
             "Gündüzleri yazılımcıyım, akşamları gitar çalıyorum. İngilizcemi konuşarak geliştirmek istiyorum.",
             [("Gitar", "Offered", "Advanced"), ("Python", "Offered", "Intermediate"),
              ("İngilizce", "Wanted", "Intermediate"), ("Fotoğrafçılık", "Wanted", "Beginner")]),
    "mert": ("Mert Aydın", "34",
             "8 yıldır İngilizce öğretmenliği yapıyorum. Gitara sıfırdan başlamak istiyorum.",
             [("İngilizce", "Offered", "Expert"), ("Satranç", "Offered", "Intermediate"),
              ("Gitar", "Wanted", "Beginner")]),
    "zeynep": ("Zeynep Kara", "06",
               "Serbest fotoğrafçıyım; düğün ve ürün çekimleri yapıyorum.",
               [("Fotoğrafçılık", "Offered", "Advanced"), ("Video düzenleme", "Offered", "Intermediate"),
                ("Gitar", "Wanted", "Beginner"), ("Python", "Wanted", "Beginner")]),
    "selin": ("Selin Arslan", "35",
              "Yoga eğitmeni ve İngilizce mütercim. Veri analizine merak sardım.",
              [("İngilizce", "Offered", "Advanced"), ("Yoga", "Offered", "Advanced"),
               ("Python", "Wanted", "Beginner")]),
    "can": ("Can Demir", "34",
            "Kadıköy'de küçük bir kahvecim var. Fotoğraf çekmeyi ve kahve demlemeyi seviyorum.",
            [("Fotoğrafçılık", "Offered", "Intermediate"), ("Kahve demleme", "Offered", "Expert"),
             ("Gitar", "Wanted", "Intermediate")]),
    "ayse": ("Ayşe Şahin", "16",
             "Konservatuvar öğrencisiyim, piyano ve İngilizce dersi verebilirim.",
             [("Piyano", "Offered", "Advanced"), ("İngilizce", "Offered", "Intermediate"),
              ("Python", "Wanted", "Beginner")]),
    "burak": ("Burak Çelik", "34",
              "Ev yemeklerine bayılırım. Gitar öğrenip mutfakta çalmak hayalim.",
              [("Türk mutfağı", "Offered", "Expert"), ("Gitar", "Wanted", "Beginner")]),
    "deniz": ("Deniz Koç", "07",
              "Almanca öğretmeniyim, Antalya'da yaşıyorum.",
              [("Almanca", "Offered", "Expert"), ("Yoga", "Wanted", "Beginner")]),
}

users = {}
for key, (name, city, bio, user_skills) in PEOPLE.items():
    username = f"{key}_ornek"
    email = f"{key}@ornek.test"
    call("POST", "/auth/register", {"email": email, "username": username, "password": PASSWORD,
                                    "ageConfirmed": True, "termsAccepted": True})
    token = None
    for _ in range(20):
        found = call("GET", f"/search?query=to:{email}", base=MAILPIT)
        if found["messages"]:
            msg = call("GET", f"/message/{found['messages'][0]['ID']}", base=MAILPIT)
            token = re.search(r"token=([A-Za-z0-9_\-%.]+)", msg["Text"]).group(1)
            break
        time.sleep(0.5)
    call("POST", "/auth/email-verification/confirm", {"token": urllib.parse.unquote(token)})
    auth = call("POST", "/auth/login", {"identifier": username, "password": PASSWORD})
    tok = auth["accessToken"]
    call("PUT", "/me/profile", {"displayName": name, "bio": bio, "city": city}, tok)
    ids = {}
    for skill, kind, level in user_skills:
        us = call("POST", "/me/skills", {"skillId": skills[skill], "type": kind, "level": level}, tok)
        ids[skill] = us["id"]
    me = call("GET", "/me", token=tok)
    users[key] = {"token": tok, "id": me["id"], "skills": ids}
    print("user", key)

T = lambda k: users[k]["token"]


def swap(sender, recipient, offered, requested, message):
    return call("POST", "/swap-requests", {"recipientId": users[recipient]["id"],
                                          "offeredSkillId": skills[offered],
                                          "requestedSkillId": skills[requested],
                                          "message": message}, T(sender))


def match(sender, recipient, offered, requested, message):
    req = swap(sender, recipient, offered, requested, message)
    return call("POST", f"/swap-requests/{req['id']}/accept", {}, T(recipient))


def hold_session(proposer, other, m, days_ago):
    """A session the two held `days_ago` days ago and both called completed."""
    # Planned a different number of days ahead each time: nobody can be in two sessions at once.
    planned = (NOW + datetime.timedelta(days=30 + days_ago)).strftime("%Y-%m-%dT%H:%M:%SZ")
    s = call("POST", f"/matches/{m['id']}/sessions", {"scheduledAt": planned, "durationMinutes": 60}, T(proposer))
    call("POST", f"/sessions/{s['id']}/accept", {}, T(other))
    held = (NOW - datetime.timedelta(days=days_ago)).strftime("%Y-%m-%d %H:%M:%S+00")
    subprocess.run(
        ["docker", "exec", POSTGRES_CONTAINER, "sh", "-c",
         f"psql -q -U \"$POSTGRES_USER\" -d {DATABASE} -c "
         f"\"UPDATE sessions.swap_sessions SET scheduled_at = '{held}' WHERE id = '{s['id']}'\""],
        check=True)
    call("POST", f"/sessions/{s['id']}/completion", {}, T(proposer))
    call("POST", f"/sessions/{s['id']}/completion/confirm", {}, T(other))


def chat(m, lines):
    for who, body in lines:
        call("POST", f"/conversations/{m['conversationId']}/messages",
             {"body": body, "clientMessageId": str(uuid.uuid4())}, T(who))
        time.sleep(1.2)


def review(author, reviewee, rating, comment):
    call("POST", "/reviews", {"revieweeId": users[reviewee]["id"], "rating": rating, "comment": comment}, T(author))


can = match("can", "elif", "Fotoğrafçılık", "Gitar", "Merhaba! Gitara ara verdim, yeniden başlamak istiyorum.")
hold_session("can", "elif", can, days_ago=12)
review("can", "elif", 9, "Dersleri çok düzenli ve hazırlıklı geliyor. Sabırla anlatıyor.")
review("elif", "can", 10, "Işığı nasıl kullanacağımı çok güzel anlattı; çektiğim fotoğraflar bambaşka oldu.")
# Profile stats count only ended matches; without one every profile would show zeros.
call("POST", f"/matches/{can['id']}/end-request", {}, T("can"))
call("POST", f"/matches/{can['id']}/end-request/accept", {}, T("elif"))

mert = match("mert", "elif", "İngilizce", "Gitar",
             "Merhaba Elif, gitara sıfırdan başlamak istiyorum. Karşılığında İngilizce konuşma pratiği yapabiliriz.")
hold_session("mert", "elif", mert, days_ago=3)
review("mert", "elif", 10, "Çok sabırlı ve anlaşılır anlatıyor. İlk derste üç akor öğrendim!")
review("elif", "mert", 9, "Konuşma pratiği tam istediğim gibiydi, hatalarımı nazikçe düzeltiyor.")
chat(mert, [
    ("elif", "Mert, ilk ders çok keyifliydi, teşekkürler!"),
    ("mert", "Asıl ben teşekkür ederim! Akorları evde çalışıyorum, parmaklarım acıyor ama ilerliyor 😅"),
    ("elif", "Normal, birkaç haftada alışırsın. Sıradaki dersi ne zaman yapalım?"),
    ("mert", "Perşembe 20:00 uygun mu? Yarım saat gitar, yarım saat İngilizce konuşma."),
    ("elif", "Süper, oturumu şimdi planlıyorum."),
    ("mert", "Harika, görüşmek üzere! See you on Thursday 👋"),
])
# The Thursday after next at 20:00 in Turkey, whenever the script runs.
thursday = (NOW + datetime.timedelta(days=7 + (3 - NOW.weekday()) % 7)).strftime("%Y-%m-%dT17:00:00Z")
upcoming = call("POST", f"/matches/{mert['id']}/sessions",
                {"scheduledAt": thursday, "durationMinutes": 60,
                 "note": "Yarım saat gitar, yarım saat İngilizce konuşma."}, T("elif"))
call("POST", f"/sessions/{upcoming['id']}/accept", {}, T("mert"))

swap("zeynep", "elif", "Fotoğrafçılık", "Gitar",
     "Merhaba Elif! Fotoğrafçılığı temelden anlatabilirim; karşılığında gitara başlamak isterim.")
swap("elif", "selin", "Python", "İngilizce", "Merhaba Selin, Python'a başlamana yardım edebilirim.")

print("elif_ornek /", PASSWORD)
