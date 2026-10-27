// The web side of the links in emails, for when the app is not installed on the device
// that opens them. With the app installed, Android opens the app instead (assetlinks.json).
// Caddy forwards these two API calls from this origin, so no CORS is needed.
(function () {
  "use strict";

  var token = new URLSearchParams(window.location.search).get("token");
  var status = document.getElementById("status");

  function show(message, isError) {
    status.textContent = message;
    status.classList.toggle("error", Boolean(isError));
    status.hidden = false;
  }

  function post(path, body) {
    return fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    }).then(function (response) {
      if (response.ok) return null;
      return response.json().then(
        function (problem) { return problem.code || "unknown"; },
        function () { return response.status === 429 ? "too_many_requests" : "unknown"; }
      );
    }, function () { return "network"; });
  }

  function messageOf(code) {
    switch (code) {
      case "invalid_token":
        return "Bu bağlantı geçersiz, daha önce kullanılmış ya da süresi dolmuş. Uygulamadan yeni bir bağlantı isteyin.";
      case "too_many_requests":
        return "Çok fazla deneme yapıldı. Birkaç dakika sonra tekrar deneyin.";
      case "network":
        return "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.";
      default:
        return "Bir sorun oluştu. Daha sonra tekrar deneyin.";
    }
  }

  function bind(form, submit) {
    if (!form) return;
    if (!token) {
      form.hidden = true;
      show(messageOf("invalid_token"), true);
      return;
    }
    form.addEventListener("submit", function (event) {
      event.preventDefault();
      var button = form.querySelector("button");
      button.disabled = true;
      submit(form).then(function (done) {
        if (done) form.hidden = true;
        else button.disabled = false;
      });
    });
  }

  bind(document.getElementById("verify-form"), function () {
    return post("/api/v1/auth/email-verification/confirm", { token: token }).then(function (code) {
      if (code) {
        show(messageOf(code), true);
        return code === "invalid_token";
      }
      show("E-posta adresiniz doğrulandı. Uygulamaya dönüp devam edebilirsiniz.");
      return true;
    });
  });

  bind(document.getElementById("reset-form"), function (form) {
    var password = form.elements["password"].value;
    if (password !== form.elements["password-again"].value) {
      show("Parolalar aynı değil.", true);
      return Promise.resolve(false);
    }
    return post("/api/v1/auth/password-reset/confirm", { token: token, newPassword: password }).then(function (code) {
      if (code === "validation_failed") {
        show("Parola en az 8, en fazla 128 karakter olmalı.", true);
        return false;
      }
      if (code) {
        show(messageOf(code), true);
        return code === "invalid_token";
      }
      show("Parolanız değiştirildi. Yeni parolanızla uygulamada giriş yapabilirsiniz.");
      return true;
    });
  });
})();
