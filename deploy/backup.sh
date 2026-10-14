#!/bin/sh
# The nightly database backup (docs/deployment.md). A dump stays on the server for 14
# days and, when a backup bucket is configured, is also copied to R2, so that losing the
# server does not lose the data. The bucket deletes copies older than 30 days itself.
set -eu
cd "$(dirname "$0")"
set -a
. ./.env
set +a

stamp=$(date -u +%Y%m%dT%H%M%SZ)
mkdir -p backups
# Written under a temporary name first, so a failed dump never looks like a backup.
docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc > "backups/.partial"
mv backups/.partial "backups/exparter-$stamp.dump"
find backups -name 'exparter-*.dump' -mtime +14 -delete

if [ -n "${BACKUP_BUCKET:-}" ]; then
  docker run --rm \
    -v "$PWD/backups:/backups:ro" \
    -e AWS_ACCESS_KEY_ID="$BACKUP_ACCESS_KEY" \
    -e AWS_SECRET_ACCESS_KEY="$BACKUP_SECRET_KEY" \
    -e AWS_DEFAULT_REGION=auto \
    amazon/aws-cli:2.38.1 \
    s3 cp "/backups/exparter-$stamp.dump" "s3://$BACKUP_BUCKET/" --endpoint-url "$STORAGE_ENDPOINT" --only-show-errors
fi
