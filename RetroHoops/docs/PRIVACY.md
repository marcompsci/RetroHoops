# Retro Hoops privacy policy

*Last updated: October 2026*

Retro Hoops is made by **Omari Bell** (contact: **rodlonbell16@gmail.com**).

**What we collect.** Outside Retro Hoops Live, nothing: there are no accounts of our own, no advertising, no analytics and no tracking. Live's ratings server collects only what's described below.

**On your device.** Your nickname, settings and career progress are saved on your iPhone or iPad. If iCloud Sync is on, a copy is kept in your own iCloud account through Apple's iCloud service, where the developer can't see it.

**Game Center (optional).** If you sign in to Game Center, Apple handles your scores, achievements and, for Retro Hoops Live, matchmaking. In a Live game, your opponent's device receives your Game Center display name, your Live rating, your chosen team and your controller input for that game. Typed text is never sent. Apple's privacy policy covers Game Center.

**Retro Hoops Live server.** The current version doesn't use it (Live ratings stay on each phone). If Live's ratings server is switched on in a later version, Live games use the developer's server (hosted on Cloudflare) to confirm who you are and keep the official ratings. The server stores your Game Center player ID and display name, your Live rating, wins and losses, the scores of your Live games. It uses these only to run Live (sign-in, ratings and the leaderboard). It never sells or shares them, and there's no advertising or tracking. Cloudflare processes requests (including your IP address) to deliver them. You can erase this data any time in the game with LIVE ▸ DELETE MY LIVE DATA.

**Two phones nearby.** When you play 2 Player on two phones, the game connects them directly over Wi-Fi or Bluetooth (Apple's Multipeer Connectivity). Only game data and the device name you see on screen are exchanged. Nothing goes over the internet.

**Purchases.** Retro Hoops Pro, a one-time purchase, is sold by Apple. The developer receives no payment details.

**Children.** Retro Hoops doesn't knowingly collect personal information from anyone, including children.

**Changes.** If this policy changes, the new version will be posted at this address with a new date.

---

### For the developer: App Store privacy answers once the Live server is on

- App Privacy ▸ Data Collection: **Yes**.
  - **Identifiers ▸ User ID** (Game Center player ID). Linked to the user. Used for App Functionality. Not used for tracking.
  - **Usage Data ▸ Product Interaction**, or **Other Data ▸ Gameplay Content**, for Live game results and ratings. Linked to the user. App Functionality. Not used for tracking.
- `Assets/Plugins/iOS/PrivacyInfo.xcprivacy`: add the matching `NSPrivacyCollectedDataTypes` entries (`NSPrivacyCollectedDataTypeUserID`, `NSPrivacyCollectedDataTypeGameplayContent`), each with linked = true, tracking = false, and purpose `NSPrivacyCollectedDataTypePurposeAppFunctionality`.
- While `BackendConfig.Url` is empty, none of this applies, and the "Data Not Collected" answer stands.

