#!/usr/bin/env bash
# Crash test of the asset operations against a real PostgreSQL (ConnectionSettings.xml: localhost:5432, database hnmo,
# which is created again). For each stop point, the worker is killed with SIGKILL there; a new process then reads the
# database and sends the same operation again. Every case must end with each jewel exactly once (vault + bank) and one
# receipt per committed operation.
set -u
cd "$(dirname "$0")"
DLL=bin/Release/AssetCrashHarness.dll
[ -f "$DLL" ] || DLL=bin/Release/net10.0/AssetCrashHarness.dll
work() { dotnet "$DLL" "$@"; }
fail=0
new_uuid() {
  if [ -r /proc/sys/kernel/random/uuid ]; then cat /proc/sys/kernel/random/uuid
  elif command -v uuidgen >/dev/null 2>&1; then uuidgen | tr '[:upper:]' '[:lower:]'
  else python -c 'import uuid; print(uuid.uuid4())'
  fi
}
check() { # expected actual label
  if [ "$1" == "$2" ]; then echo "  ok   $3: $2"; else echo "  FAIL $3: expected '$1', got '$2'"; fail=1; fi
}
killed_at() { # stage command operation
  local flag; flag=$(mktemp -u)
  # exec: $! is the worker itself, so the SIGKILL ends it (and its open transaction) for real.
  ( export HNMO_TEST_ASSET_BARRIER="$1" HNMO_TEST_ASSET_BARRIER_FILE="$flag"; exec dotnet "$DLL" "$2" "$3" > /dev/null 2>&1 ) &
  local pid=$!
  for _ in $(seq 1 600); do [ -f "$flag" ] && break; sleep 0.1; done
  [ -f "$flag" ] || { echo "  FAIL the worker never reached $1"; fail=1; }
  kill -9 "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; rm -f "$flag"
  if kill -0 "$pid" 2>/dev/null; then echo "  FAIL the worker survived the kill"; fail=1; fi
}

work setup | tail -1
for stage in before-save before-commit after-commit; do
  echo "deposit killed at $stage"
  work reset > /dev/null
  op=$(new_uuid)
  killed_at "$stage" deposit "$op"
  after=$(work state "$op")
  if [ "$stage" == "after-commit" ]; then check "vault 0 bank 4 receipts 1" "$after" "after the kill"; else check "vault 4 bank 0 receipts 0" "$after" "after the kill"; fi
  echo "  retry: $(work deposit "$op")"
  check "vault 0 bank 4 receipts 1" "$(work state "$op")" "after the retry"
  echo "  second retry: $(work deposit "$op")"
  check "vault 0 bank 4 receipts 1" "$(work state "$op")" "after a second retry"

  echo "withdraw killed at $stage"
  op2=$(new_uuid)
  killed_at "$stage" withdraw "$op2"
  after=$(work state "$op2")
  if [ "$stage" == "after-commit" ]; then check "vault 3 bank 1 receipts 1" "$after" "after the kill"; else check "vault 0 bank 4 receipts 0" "$after" "after the kill"; fi
done

echo "two retries of the same deposit at once"
work reset > /dev/null
op=$(new_uuid)
work deposit "$op" > /tmp/hnmo-crash-a.txt 2>&1 & a=$!
work deposit "$op" > /tmp/hnmo-crash-b.txt 2>&1 & b=$!
wait $a $b
echo "  answers: $(cat /tmp/hnmo-crash-a.txt) / $(cat /tmp/hnmo-crash-b.txt)"
check "vault 0 bank 4 receipts 1" "$(work state "$op")" "after both"

echo "the database connection is cut right before the commit"
work reset > /dev/null
op=$(new_uuid)
flag=$(mktemp -u); go=$(mktemp -u)
HNMO_TEST_ASSET_BARRIER=before-commit HNMO_TEST_ASSET_BARRIER_FILE="$flag" HNMO_TEST_ASSET_BARRIER_GO="$go" work deposit "$op" > /tmp/hnmo-crash-c.txt 2>&1 & c=$!
for _ in $(seq 1 600); do [ -f "$flag" ] && break; sleep 0.1; done
PGPASSWORD=admin psql -h 127.0.0.1 -U postgres -d hnmo -qAt -c "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname='hnmo' AND pid <> pg_backend_pid() AND state = 'idle in transaction';" | sed 's/^/  connections cut: /'
touch "$go"; wait $c
echo "  worker: $(cat /tmp/hnmo-crash-c.txt)"
check "vault 4 bank 0 receipts 0" "$(work state "$op")" "after the cut"
echo "  retry: $(work deposit "$op")"
check "vault 0 bank 4 receipts 1" "$(work state "$op")" "after the retry"
rm -f "$flag" "$go"

echo "=== two-database shared coin 2PC crash tests (hnmo <-> hnmo_s9) ==="
coin_work() { HNMO_SEASON=s9 HNMO_REALM_ID=s9 work "$@"; }
work coin-setup | tail -1
coin_work coin-setup | tail -1
for stage in before-prepare after-prepare before-local-commit after-local-commit before-canonical-commit after-canonical-commit; do
  echo "coin-spend killed at $stage"
  coin_work coin-reset > /dev/null
  op=$(new_uuid)
  flag=$(mktemp -u)
  ( export HNMO_SEASON=s9 HNMO_REALM_ID=s9 HNMO_TEST_ASSET_BARRIER="$stage" HNMO_TEST_ASSET_BARRIER_FILE="$flag"; exec dotnet "$DLL" coin-spend "$op" 40 s9 > /dev/null 2>&1 ) &
  pid=$!
  for _ in $(seq 1 600); do [ -f "$flag" ] && break; sleep 0.1; done
  [ -f "$flag" ] || { echo "  FAIL never reached $stage"; fail=1; }
  kill -9 "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; rm -f "$flag"
  # Unreadable local DB keeps prepared transaction pending when prepared exists
  HNMO_TEST_LOCAL_OVERRIDE_CONN="127.0.0.1:59999" coin_work coin-recover s9 > /dev/null
  coin_work coin-recover s9 > /dev/null
  state=$(coin_work coin-state "$op")
  if [ "$stage" == "after-local-commit" ] || [ "$stage" == "before-canonical-commit" ] || [ "$stage" == "after-canonical-commit" ]; then
    check "canon=60 local=60 items=1 receipts=1 prepared=0" "$state" "recovered commit ($stage)"
  else
    check "canon=100 local=100 items=0 receipts=0 prepared=0" "$state" "recovered rollback ($stage)"
  fi
done

echo "insufficient shared balance refused in full"
coin_work coin-reset > /dev/null
op_ins=$(new_uuid)
out_ins=$(coin_work coin-spend "$op_ins" 150 s9)
check "outcome=Refused afterC=0" "$out_ins" "refused in full"
check "canon=100 local=100 items=0 receipts=0 prepared=0" "$(coin_work coin-state "$op_ins")" "state untouched on refusal"

[ $fail -eq 0 ] && echo "ALL PASSED" || echo "SOME FAILED"
exit $fail
