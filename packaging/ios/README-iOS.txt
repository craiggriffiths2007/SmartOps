SMARTOPS SURVEY - UNSIGNED iOS DEVICE BUILD

This folder contains signing instructions and (after a successful SmartOps.Survey
iOS workflow run) an unsigned iPhone/iPad device build:
  SmartOps-iOS-net11-unsigned.ipa
  SmartOps-iOS-net11-unsigned-app.zip

These files are built for real iPhone/iPad devices (ios-arm64), NOT the simulator.
They are NOT signed, cannot be installed directly and are NOT App Store releases.
The bundle identifier in the iOS project is uk.co.ssmartops.survey.

SOURCE AND LATEST UNSIGNED BUILD
https://github.com/craiggriffiths2007/SmartOps.Survey
https://github.com/craiggriffiths2007/SmartOps.Survey/releases/tag/ios-device-unsigned

SIGNING AND INSTALLATION (BY THE PERSON INSTALLING THE APP)

1. Obtain appropriate Apple signing credentials and provisioning for the iOS
   device and bundle identifier. A paid Apple Developer membership may be
   needed depending on the chosen distribution method.
2. On a Mac with compatible Xcode, the recommended development approach is to
   clone SmartOps.Survey, configure the signing team, bundle identifier and
   provisioning profile for your own account, then build/install using the
   corresponding iOS project. This rebuilds from source rather than modifying
   the distributed unsigned binary.
3. Alternatively, an experienced developer can re-sign the unsigned .app/.ipa
   with an appropriate Apple distribution or development certificate, matching
   entitlements and an embedded provisioning profile, then install it using an
   Apple-supported method. Exact steps depend on the certificate/profile.
4. Enable Developer Mode or trust the developer if required by the device.
5. Connect the installed app to the SmartOps 4D server on your trusted LAN:
   http://YOUR-PC-IP:8045 (agent code such as ADMIN).

IMPORTANT
* A downloaded unsigned IPA is NOT a one-click installer on iOS.
* Installing an app with an Apple account or third-party sideloading utility is
  subject to Apple's provisioning, device and account limitations.
* Changing the app's bundle identifier may be required for your signing team;
  in that case rebuild from source and use your own bundle identifier.
* App runtime behaviour on iPhone/iPad (camera, media, network, permissions)
  has not been validated by an on-device SmartOps release test.
* Do not expose the demo 4D server or its test credentials to the internet.
* The Android APK in SmartOps.Android/ can be installed separately without Apple tools.
