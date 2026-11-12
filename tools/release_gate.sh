#!/bin/bash
# Retro Hoops release gate (16 Oct 2026 Apple compliance gate). Runs every automated check that can run without
# Unity or Xcode, and prints the manual items that still need a person. Exit code 0 only if every automated check
# passes. A pass here is NOT App Store approval and NOT a device test: see docs/testflight-release-checklist.md.
#   bash tools/release_gate.sh
set -u
cd "$(dirname "$0")/.."
status=0
notrun=2 # Xcode archive/upload and device testing never run here
step() { echo; echo "== $1"; }

step "Logic tests (engine-free C#, tools/LogicTests)"
if command -v dotnet >/dev/null; then
  (cd tools/LogicTests && dotnet run -c Release --project Runner) > /tmp/rh_logic.txt 2>&1
  rc=$?; tail -3 /tmp/rh_logic.txt
  if [ $rc -ne 0 ]; then echo "FAIL: logic tests"; grep "FAIL " /tmp/rh_logic.txt | head -20; status=1; fi
else
  echo "SKIPPED: dotnet not installed (this is NOT a pass)"; status=1
fi

step "Text audit (glyphs, Spanish strings)"
python3 tools/TextAudit/text_audit.py --strict | tail -4
[ ${PIPESTATUS[0]} -eq 0 ] || { echo "FAIL: text audit"; status=1; }

step "App Store audit (repo-side Apple requirements)"
python3 tools/AppStoreAudit/app_store_audit.py | grep -v "^PASS"
[ ${PIPESTATUS[0]} -eq 0 ] || { echo "FAIL: App Store audit"; status=1; }

step "Unity compile check (UnityCheck, iOS defines)"
# 12 Nov 2026: runs when UNITY_REFS points at Unity 6000.6.3f1's reference assemblies (see tools/UnityCheck). Otherwise NOT RUN.
if [ -n "${UNITY_REFS:-}" ] && [ -n "${UNITYCHECK_DIR:-}" ] && command -v dotnet >/dev/null; then
  assets="$PWD/RetroHoops/Assets"
  (cd "$UNITYCHECK_DIR" && dotnet run -c Release --no-build -- "$UNITY_REFS" "$assets" ios) > /tmp/rh_unity.txt 2>&1
  grep -E "files," /tmp/rh_unity.txt
  if grep -qE "[1-9][0-9]* errors" /tmp/rh_unity.txt || ! grep -q "files," /tmp/rh_unity.txt; then echo "FAIL: UnityCheck"; status=1; fi
else
  echo "NOT RUN: UnityCheck (set UNITY_REFS and UNITYCHECK_DIR). This is NOT a pass."; notrun=$((notrun+1))
fi

step "Not run here"
echo "NOT RUN: Xcode archive, signing, upload. NOT RUN: any device test."
echo
if [ $status -eq 0 ]; then echo "RELEASE GATE: every check that ran passed; $notrun check(s) NOT RUN and the MANUAL items above still need a person. This is not a release PASS."
else echo "RELEASE GATE: FAILED"; fi
exit $status
