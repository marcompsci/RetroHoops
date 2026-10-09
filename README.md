# Retro Hoops — Retro Hoops

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade 3v3 basketball game for iPhone, built in Unity 6 LTS.

- **Unity project:** [`RetroHoops/`](RetroHoops/) — setup, controls, iOS build steps, and architecture are in [`RetroHoops/README.md`](RetroHoops/README.md).
- **Plan and phase log:** [`RetroHoops/docs/PLAN.md`](RetroHoops/docs/PLAN.md)
- **Design:** [`RetroHoops/DESIGN.md`](RetroHoops/DESIGN.md)
- **Release checklist and App Store kit:** [`RetroHoops/docs/RELEASE_CHECKLIST.md`](RetroHoops/docs/RELEASE_CHECKLIST.md), [`RetroHoops/docs/APP_STORE.md`](RetroHoops/docs/APP_STORE.md)
- **Game Center setup:** [`RetroHoops/docs/GAME_CENTER.md`](RetroHoops/docs/GAME_CENTER.md)
- **Manual QA checklist:** [`RetroHoops/docs/QA_CHECKLIST.md`](RetroHoops/docs/QA_CHECKLIST.md)
- **Command-line iOS build (macOS, Unity 6 installed):** `tools/build_ios.sh [simulator|device]`
- **Logic tests without Unity:** `cd tools/LogicTests && dotnet run --project Runner/Runner.csproj`

**Status:** Phases 1–41 implemented (Phase 41: launch mode, with LIVE free, a one-time $3.99 Retro Hoops Pro unlock and a feature freeze; Phase 40: release candidate checks, an App Store audit with support and privacy pages, Season 12 Putt Club, a HIGHLIGHTS reel and SHARE RESULT card, in-game scouting and SMART DIFFICULTY; Phase 39: JUST PLAY first game, a NEXT suggestion and a welcome-back bonus, Season 11 Sky Kites, stick size / dead zone / haptics settings; Phase 38: DAILY CLUTCH, a CLUTCH scenario editor with share codes, Season 10 Wash House, a visible shot contest, CLUTCH balance, App Store screenshot fixes; Phase 37: CLUTCH scenarios, Season 9, text audit). Game logic passes 815 automated tests on .NET and the Live server 23, and every assembly compiles against Unity 6000.6.3f1's reference assemblies with 0 errors, including the iOS player code paths (tools/UnityCheck). The Phase 18 build ran on an iPhone and the Phase 25 build archived in Xcode; Phases 26–36 compiled in Unity and Xcode and launched in the iOS Simulator on 6 Oct 2026 (menus and text render; Phase 37 also passed the Build Check on 7 Oct; Phase 38 too; Phase 40 passed the Build Check on 7 Oct and was uploaded as build 3; Phase 41 not compiled there yet) but haven't run on a device (iPad and Mac never tested), and build 3 (Phase 40) is uploaded to App Store Connect.

All teams, players, courts, logos, art, and audio are original and generated procedurally in the project.
