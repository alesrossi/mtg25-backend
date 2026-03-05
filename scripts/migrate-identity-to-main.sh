#!/bin/bash
set -e

CONTAINER="mtg25-backend-postgres-1"
PG_USER="root"
PG_PASS="supersecretlongpassword"
SRC_DB="identity"
DST_DB="main"

TABLES=(
  '"AspNetUsers"'
  '"AspNetRoles"'
  '"AspNetRoleClaims"'
  '"AspNetUserClaims"'
  '"AspNetUserLogins"'
  '"AspNetUserRoles"'
  '"Leagues"'
  '"AppUserFriends"'
  '"LeagueRoleAssignments"'
  '"Notifications"'
  '"Rounds"'
  '"RoundParticipants"'
  '"Settings"'
  '"UserLeagues"'
  '"UserRounds"'
)

TABLE_FLAGS=""
for t in "${TABLES[@]}"; do
  TABLE_FLAGS="$TABLE_FLAGS -t '$t'"
done

echo "Migrating data from '$SRC_DB' to '$DST_DB'..."

docker exec "$CONTAINER" bash -c "
  PGPASSWORD=$PG_PASS pg_dump -U $PG_USER -d $SRC_DB --data-only --no-owner --no-acl $TABLE_FLAGS \
  | PGPASSWORD=$PG_PASS psql -U $PG_USER -d $DST_DB
"

echo "Done."