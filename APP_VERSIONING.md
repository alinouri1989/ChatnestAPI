# ChatNest Application Versioning

## Architecture

All clients call this anonymous .NET REST endpoint at startup and on foreground resume:

```http
GET /api/app-version?platform=pwa&build=130&version=2.5.0&channel=production
```

SignalR is not involved. The API reads the matching `(platform, channel)` row from SQL Server and compares integer builds.

## Database model

`AppVersionPolicies` stores latest/minimum builds, display versions, maintenance state, text, store URL, and reminder interval. `AppVersionReleaseNotes` stores ordered notes linked by `AppVersionPolicyId`.

Migration `AddAppVersionPolicies` seeds production policies for `android`, `ios`, and `pwa`. Replace the placeholder iOS App Store ID before release.

Decision precedence:

1. `Maintenance = true` → `maintenance`
2. Build below `MinimumSupportedBuild` → `required`
3. Build below `LatestBuild` → `optional`
4. Otherwise → `none`

## Response and errors

```json
{
  "platform": "pwa",
  "updateAvailable": true,
  "updateType": "optional",
  "current": { "version": "2.4.0", "build": 125 },
  "latest": { "version": "2.5.0", "build": 130 },
  "minimumSupported": { "version": "2.3.0", "build": 110 },
  "title": "A new ChatNest version is available",
  "message": "Reload ChatNest to use the latest version.",
  "releaseNotes": ["Improved chat performance"],
  "storeUrl": "https://app.chatnest.ir",
  "remindAfterSeconds": 86400
}
```

Invalid query parameters return `400`; an unknown platform/channel returns `404`.

## Client behavior

| Type | Android | iOS | PWA |
|---|---|---|---|
| `none` | Continue | Continue | Continue |
| `optional` | Flexible update | Dismissible App Store prompt | Dismissible update dialog |
| `required` | Immediate update | Non-dismissible App Store screen | Non-dismissible update screen |
| `maintenance` | Block and retry | Block and retry | Block and retry |

The PWA build comes from `chatnest.ui/package.json`. Its service-worker cache version must advance with each deployment. A waiting worker activates only after the user chooses update.

## Release checklist

1. Build and deploy the new client or publish the store release.
2. Update `LatestVersion` and `LatestBuild` for its database policy.
3. Replace release-note rows and preserve their unique `DisplayOrder` values.
4. Keep the previous minimum build during an optional rollout.
5. Raise `MinimumSupportedBuild` only when older clients must be blocked.
6. Test builds below minimum, between minimum/latest, and at latest.
