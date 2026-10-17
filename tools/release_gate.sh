#!/bin/bash
# Retro Hoops release gate (16 Oct 2026 Apple compliance gate). Runs every automated check that can run without
# Unity or Xcode, and prints the manual items that still need a person. Exit code 0 only if every automated check
# passes. A pass here is NOT App Store approval and NOT a device test: see docs/testflight-release-checklist.md.
#   bash tools/release_gate.sh
set -u
cd "$(dirname "$0")/.."
status=0
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
python3 tools/TextAudit/text_audit.py | tail -4
[ ${PIPESTATUS[0]} -eq 0 ] || { echo "FAIL: text audit"; status=1; }

step "App Store audit (repo-side Apple requirements)"
python3 tools/AppStoreAudit/app_store_audit.py | grep -v "^PASS"
[ ${PIPESTATUS[0]} -eq 0 ] || { echo "FAIL: App Store audit"; status=1; }

step "Not run here"
echo "NOT RUN: Unity compile (UnityCheck needs Unity's reference assemblies; Unity builds it on the Mac)."
echo "NOT RUN: Xcode archive, signing, upload. NOT RUN: any device test."
echo
if [ $status -eq 0 ]; then echo "RELEASE GATE: automated checks pass. Manual items above still need a person."
else echo "RELEASE GATE: FAILED"; fi
exit $status
