# Dynamic Island (Windows) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A WPF tray app that shows a macOS-style notch ("island") at the top-center of the primary monitor for now-playing media and incoming Windows notifications, hidden when idle and whenever a fullscreen app is in front.

**Architecture:** All decision logic (state machine, spring physics, notch geometry, fullscreen rules, notification mapping/dedupe, settings) lives in a pure `DynamicIsland.Core` library with xUnit tests. The WPF app (`DynamicIsland`) holds thin Windows adapters (GSMTC, UserNotificationListener, Win32 hooks) that feed events into the Core state machine on the UI thread, and a single borderless island window that renders the state. Package identity (required by UserNotificationListener) comes from a **sparse package** registered with `-ExternalLocation`, built by a PowerShell script.

**Tech Stack:** .NET SDK 11.0.100, C#, WPF (+ WinForms only for `NotifyIcon`), WinRT projections via `net11.0-windows10.0.19041.0`, xUnit, Windows SDK 10.0.26100 (`makeappx.exe`, `signtool.exe` in `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\`).

**Spec:** `docs/superpowers/specs/2026-11-17-dynamic-island-design.md` (Turkish). Executors read both.

## Global Constraints

- Core library: `net11.0`, no WPF/WinRT references. App: `net11.0-windows10.0.19041.0`, `UseWPF=true`, `UseWindowsForms=true`, `SupportedOSPlatformVersion=10.0.19041.0`.
- Sizes at 100% scale (DIPs): compact 220×34, expanded 420×110, notification 400×80. Top flare radius 10. Window always on **primary monitor**, top edge y=0, horizontally centered.
- Timings: hover delay 250 ms, collapse after pointer leave 500 ms, hide after pause 5 s, notification visible 5 s (frozen while hovered).
- Island background pure black `#000000`, text white / `#A0A0A0`, font `Segoe UI Variable`.
- Island window must never take focus (`WS_EX_NOACTIVATE`), never appear in Alt+Tab/taskbar (`WS_EX_TOOLWINDOW`, `ShowInTaskbar=false`), and must pass clicks through everywhere outside the visible notch.
- While a fullscreen app is in front: island hidden; notifications arriving then are dropped, never replayed.
- UI language: Turkish (tray menu, onboarding text).
- Personal use only: self-signed cert, no installer/updater/Store. Published app lives in `%LOCALAPPDATA%\DynamicIsland\app` (not under OneDrive).
- Settings file: `%LOCALAPPDATA%\DynamicIsland\settings.json`.
- Package identity names (must match between `AppxManifest.xml` and the exe's `app.manifest`): Name `DynamicIsland`, Publisher `CN=DynamicIslandDev`, Application Id `DynamicIsland`.
- All number→string formatting in Core uses `CultureInfo.InvariantCulture` (the user's machine is Turkish: decimal comma).

## Review Focus

1. **Turkish locale (decimal comma):** notch path data built on a `tr-TR` thread must still parse (`10.5`, not `10,5`). Test in Task 3.
2. **Startup replay:** notifications already sitting in the notification center when the app starts must not be shown. Test in Task 5.
3. **Same notification seen twice** (event + poll, or poll twice): shown once. Test in Task 5.
4. **Maximized window with auto-hide taskbar** covers the monitor but is not fullscreen; island must stay usable. Test in Task 4.
5. **Media source vanishes** (Spotify closed) while island expanded/hovered: island hides, no stale cover. Test in Task 2. Plus: running **without package identity or with access denied**: app runs music-only, no crash (manual check in Task 7).

---

## File Structure

```
DynamicIsland.sln
.gitignore                                   bin/ obj/ artifacts/ *.pfx
src/DynamicIsland.Core/
  Models.cs                 MediaInfo, IslandNotification, IslandMode, IslandTimings
  IslandStateMachine.cs     all show/hide/expand/queue decisions
  Spring.cs                 damped spring integrator
  NotchGeometry.cs          path-data string for the notch shape
  FullscreenRules.cs        pure fullscreen decision
  NotificationMapper.cs     toast texts → IslandNotification; filtering
  SeenNotifications.cs      dedupe of notification ids
  AppSettings.cs            load/save JSON with defaults
tests/DynamicIsland.Core.Tests/
  IslandStateMachineTests.cs SpringTests.cs NotchGeometryTests.cs
  FullscreenRulesTests.cs NotificationMapperTests.cs SeenNotificationsTests.cs AppSettingsTests.cs
src/DynamicIsland/
  app.manifest              msix identity element + PerMonitorV2 DPI
  App.xaml(.cs)             single instance, wiring, tick timer
  Interop/Native.cs         all P/Invoke
  Services/MediaService.cs  GSMTC adapter
  Services/NotificationService.cs  UserNotificationListener adapter (event + 1 s poll)
  Services/FullscreenMonitor.cs    WinEvent hook + 1 s poll → FullscreenRules
  Services/AppLauncher.cs          AUMID activation
  Services/NotificationSound.cs
  Shell/TrayIcon.cs         NotifyIcon + Turkish menu
  Shell/StartupRegistration.cs     HKCU Run key
  Shell/OnboardingWindow.xaml(.cs)
  Island/IslandWindow.xaml(.cs)    window styles, positioning, click-through, springs
  Island/MusicCompactView.xaml MusicExpandedView.xaml NotificationView.xaml
  Island/Equalizer.xaml
packaging/
  AppxManifest.xml  Assets/Logo.png (50×50) Assets/Square44.png Assets/Square150.png
  build.ps1         publish → pack → sign → register
  create-cert.ps1   one-time cert (needs admin for TrustedPeople import)
spike/NotifSpike/   throwaway, deleted after Task 1
```

---

### Task 1: Scaffold, package identity, and spike

**Files:**
- Create: `DynamicIsland.sln`, `.gitignore`, `src/DynamicIsland.Core/DynamicIsland.Core.csproj`, `tests/DynamicIsland.Core.Tests/DynamicIsland.Core.Tests.csproj`, `src/DynamicIsland/DynamicIsland.csproj`, `src/DynamicIsland/app.manifest`, `packaging/*`
- Modify: `docs/superpowers/specs/2026-11-17-dynamic-island-design.md` (spike results section)

**Interfaces:**
- Produces: buildable solution; `packaging/build.ps1 [-Configuration Release]` that leaves the app registered with identity; app exe at `%LOCALAPPDATA%\DynamicIsland\app\DynamicIsland.exe`.

- [ ] **Step 1:** `git init`, `.gitignore`, create the three projects with `dotnet new classlib|xunit|wpf`, add to sln, test project references Core, app references Core. Delete template `Class1.cs`/`UnitTest1.cs`.
- [ ] **Step 2:** Run `dotnet build` and `dotnet test`. Expected: build succeeded, 0 tests.
- [ ] **Step 3:** Write `packaging/AppxManifest.xml` for a sparse package: `Identity Name="DynamicIsland" Publisher="CN=DynamicIslandDev" Version="1.0.0.0" ProcessorArchitecture="x64"`; `<uap10:AllowExternalContent>true</uap10:AllowExternalContent>`; TargetDeviceFamily `Windows.Desktop` MinVersion `10.0.19041.0`; capabilities `rescap:runFullTrust`, `rescap:unvirtualizedResources`, `uap3:Capability Name="userNotificationListener"`; `Application Id="DynamicIsland" Executable="DynamicIsland.exe" uap10:TrustLevel="mediumIL" uap10:RuntimeBehavior="win32App"` with `uap:VisualElements AppListEntry="none"`. Add `app.manifest` to the WPF project with `<msix xmlns="urn:schemas-microsoft-com:msix.v1" publisher="CN=DynamicIslandDev" packageName="DynamicIsland" applicationId="DynamicIsland"/>` and `PerMonitorV2` dpiAwareness. Placeholder PNG assets.
- [ ] **Step 4:** `packaging/create-cert.ps1`: `New-SelfSignedCertificate -Type Custom -Subject "CN=DynamicIslandDev" -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")`, export to `artifacts/dev.pfx` (password `dev`), import the .cer into `Cert:\LocalMachine\TrustedPeople` (script checks for admin and tells the user to re-run elevated if not). **The user runs this once** (`! powershell -ExecutionPolicy Bypass -File packaging/create-cert.ps1` from an elevated terminal).
- [ ] **Step 5:** `packaging/build.ps1`: stop running `DynamicIsland` process; `dotnet publish src/DynamicIsland -c Release -r win-x64 --self-contained false -o $env:LOCALAPPDATA\DynamicIsland\app`; `makeappx pack /d packaging /p artifacts\DynamicIsland.msix /nv /o` (only manifest + Assets); `signtool sign /fd SHA256 /f artifacts\dev.pfx /p dev artifacts\DynamicIsland.msix`; `Remove-AppxPackage` any previous `DynamicIsland*`; `Add-AppxPackage -Path artifacts\DynamicIsland.msix -ExternalLocation $env:LOCALAPPDATA\DynamicIsland\app`.
- [ ] **Step 6:** Spike: temporarily make the WPF app's `App.OnStartup` (a) print `Windows.ApplicationModel.Package.Current.Id.FullName` (catch → "no identity"), (b) `UserNotificationListener.Current.RequestAccessAsync()`, subscribe to `NotificationChanged` (catch & log exceptions) and poll `GetNotificationsAsync(NotificationKinds.Toast)` every 1 s logging new ids + texts, (c) log GSMTC `GetCurrentSession()` media properties on change. Log to `%LOCALAPPDATA%\DynamicIsland\spike.log`.
- [ ] **Step 7:** Run `build.ps1`, launch the exe, then with the user: trigger a real notification (a WhatsApp/Discord message to themselves) with DND **off**, then again with DND **on**; play Spotify and a YouTube tab. Expected in log: identity full name; access `Allowed`; notification texts in both DND states; media title/artist. Record for each: does `NotificationChanged` fire, or only polling?
- [ ] **Step 8:** Write results into the spec under a new "## 10. Spike sonuçları" section. If DND blocks the listener, note that onboarding (Task 9) uses the per-app banner-off fallback text. Remove spike code from `App.xaml.cs`.
- [ ] **Step 9:** Commit `chore: scaffold solution and sparse package identity`.

---

### Task 2: Island state machine

**Files:**
- Create: `src/DynamicIsland.Core/Models.cs`, `src/DynamicIsland.Core/IslandStateMachine.cs`
- Test: `tests/DynamicIsland.Core.Tests/IslandStateMachineTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum IslandMode { Hidden, MusicCompact, MusicExpanded, Notification }
public sealed record MediaInfo(string Title, string Artist, bool IsPlaying,
    TimeSpan Position, TimeSpan Duration, DateTimeOffset PositionUpdatedAt, byte[]? Thumbnail);
public sealed record IslandNotification(string Id, string AppName, string? AppUserModelId,
    string Title, string Body, byte[]? AppLogo);
public sealed record IslandTimings(TimeSpan HoverDelay, TimeSpan CollapseDelay,
    TimeSpan PausedHide, TimeSpan NotificationDuration)
{ public static IslandTimings Default { get; } /* 250ms, 500ms, 5s, 5s */ }

public sealed class IslandStateMachine
{
    public IslandStateMachine(IslandTimings timings);
    public IslandMode Mode { get; }
    public MediaInfo? Media { get; }
    public IslandNotification? CurrentNotification { get; }
    public event Action? Changed;                 // raised only when Mode, Media or CurrentNotification changes
    public void OnMediaChanged(MediaInfo? media, DateTimeOffset now);
    public void OnNotification(IslandNotification n, DateTimeOffset now);
    public void OnPointerEnter(DateTimeOffset now);
    public void OnPointerLeave(DateTimeOffset now);
    public IslandNotification? OnClick(DateTimeOffset now); // returns notification to activate, if one was clicked
    public void OnFullscreenChanged(bool isFullscreen, DateTimeOffset now);
    public void Tick(DateTimeOffset now);         // drives all timers; no internal clocks
}
```

Rules: "music visible" = media != null and (playing, or paused less than `PausedHide` ago, or pointer over). Base mode = music visible ? (pointer over and hover delay elapsed, or clicked → Expanded : Compact) : Hidden. Notification preempts base mode; queue FIFO; after a notification ends, next queued one shows, else base mode. Fullscreen forces Hidden, drops current + queued notifications and ignores new ones; media keeps being tracked.

- [ ] **Step 1: Write the failing tests** (use a `t0` and `t0 + ms(x)`; helper `Playing()`/`Paused()` MediaInfo builders):
  - `StartsHidden` → `Mode == Hidden`
  - `PlayingMedia_ShowsCompact`
  - `Hover_ExpandsOnlyAfterDelay` → enter at t0; Tick(t0+200ms) Compact; Tick(t0+260ms) Expanded
  - `HoverLeave_CollapsesAfterDelay` → Expanded, leave at t1; Tick(t1+400ms) Expanded; Tick(t1+510ms) Compact
  - `Click_OnCompact_ExpandsImmediately_ReturnsNull`
  - `Paused_HidesAfter5s` → Tick(+4.9s) Compact; Tick(+5.1s) Hidden
  - `Paused_ThenResumed_DoesNotHide`
  - `Paused_WhilePointerOver_DoesNotHide_UntilLeave`
  - `MediaNull_WhileExpanded_HidesImmediately_AndClearsMedia` (Review Focus 5) → `Mode == Hidden`, `Media == null`
  - `Notification_ShowsThenReturnsToCompact` → Tick(+4.9s) Notification; Tick(+5.1s) Compact
  - `Notification_WithNoMedia_ReturnsToHidden`
  - `Notifications_AreQueued_FIFO` → n1 then n2 at t0; at +5.1s Current is n2; at +10.2s base mode
  - `Notification_HoverFreezesTimer` → enter at +2s, Tick(+20s) still Notification; leave at +20s; Tick(+22.9s) Notification; Tick(+23.1s) gone
  - `Click_OnNotification_ReturnsIt_AndDismisses`
  - `Fullscreen_HidesEverything_DropsQueue_IgnoresNew` → after exit with media playing: Compact, `CurrentNotification == null` even though n2 arrived during fullscreen
  - `Changed_RaisedOnlyOnActualChange` → two identical Ticks raise once
- [ ] **Step 2:** `dotnet test --filter IslandStateMachineTests` → FAIL (types missing).
- [ ] **Step 3:** Implement `Models.cs` and `IslandStateMachine` per the rules above. Store absolute deadlines; for the hover freeze store remaining time on enter and a new deadline on leave.
- [ ] **Step 4:** `dotnet test --filter IslandStateMachineTests` → all PASS.
- [ ] **Step 5:** Commit `feat(core): island state machine`.

---

### Task 3: Spring and notch geometry

**Files:**
- Create: `src/DynamicIsland.Core/Spring.cs`, `src/DynamicIsland.Core/NotchGeometry.cs`
- Test: `tests/DynamicIsland.Core.Tests/SpringTests.cs`, `NotchGeometryTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class Spring
{
    public Spring(double value, double stiffness = 280, double damping = 22); // mass 1, ζ≈0.66 → ~6% overshoot
    public double Value { get; } public double Velocity { get; } public double Target { get; set; }
    public bool IsSettled { get; }               // |Target-Value| < 0.05 && |Velocity| < 0.05
    public void Step(double dtSeconds);          // semi-implicit Euler, internally sub-steps at ≤ 1/240 s
}
public static class NotchGeometry
{
    // Total shape width = bodyWidth + 2*flare. Top edge at y=0. Returns WPF path mini-language.
    public static string BuildPathData(double bodyWidth, double height, double flare, double bottomRadius);
}
```
Shape (x grows right): `M0,0` → quarter curve to `(flare, flare)` (concave flare) → line to `(flare, height-bottomRadius)` → arc to `(flare+bottomRadius, height)` → line to `(flare+bodyWidth-bottomRadius, height)` → arc to `(flare+bodyWidth, height-bottomRadius)` → line to `(flare+bodyWidth, flare)` → curve to `(bodyWidth+2*flare, 0)` → `Z`. `bottomRadius` clamped to `min(bottomRadius, height-flare, bodyWidth/2)`.

- [ ] **Step 1: Write failing tests:**
  - `Spring_ConvergesWithin1s` → `new Spring(0){Target=100}`, step 60×(1/60) → `IsSettled`, `|Value-100| < 0.5`
  - `Spring_Overshoots` → max Value over 1 s `> 100` and `< 110`
  - `Spring_LargeDt_IsStable` → single `Step(0.5)` keeps Value finite and `< 200`
  - `Notch_StartsAtOriginAndClosesAtFullWidth` → `BuildPathData(200,34,10,17)` starts with `"M0,0"`, contains `"220,0"`, ends with `"Z"`
  - `Notch_UsesInvariantCulture_UnderTurkish` (Review Focus 1) → with `CultureInfo.CurrentCulture = new("tr-TR")` (restore in `finally`), `BuildPathData(200.5,34,10,17)` contains `"210.5,"` (the right edge x = flare+bodyWidth) and every number extracted with regex `-?\d+(\.\d+)?` round-trips through `double.Parse(s, CultureInfo.InvariantCulture)`; also assert the count of numbers equals the count the builder's own format produces under `InvariantCulture` (same string under both cultures).
  - `Notch_ClampsRadius` → `BuildPathData(20,15,10,17)` does not throw and contains no negative numbers.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement both.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): spring physics and notch geometry`.

---

### Task 4: Fullscreen rules

**Files:**
- Create: `src/DynamicIsland.Core/FullscreenRules.cs`
- Test: `tests/DynamicIsland.Core.Tests/FullscreenRulesTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum UserNotificationState { NotPresent = 1, Busy = 2, RunningD3DFullScreen = 3, PresentationMode = 4, AcceptsNotifications = 5, QuietTime = 6, App = 7 }
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom);
public sealed record ForegroundWindow(string ClassName, PixelRect Bounds, bool IsMaximized, bool IsOwnProcess);
public static class FullscreenRules
{
    public static bool IsFullscreen(UserNotificationState quns, ForegroundWindow? fg, PixelRect monitorOfFg);
}
```
True if quns ∈ {Busy, RunningD3DFullScreen, PresentationMode}; else true if fg != null, not own process, class ∉ {`Progman`, `WorkerW`, `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`}, **not maximized**, and Bounds contains monitor rect. `Bounds` are DWM extended-frame bounds (caller's job).

- [ ] **Step 1: Write failing tests:** `D3DFullscreen_True`, `PresentationMode_True`, `BorderlessGameCoveringMonitor_True` (`"UnityWndClass"`, bounds == monitor), `BrowserF11_True` (`"Chrome_WidgetWin_1"`, bounds == monitor, not maximized), `MaximizedWithAutoHideTaskbar_False` (Review Focus 4: bounds == monitor, `IsMaximized=true`), `NormalMaximizedWindow_False` (bounds smaller than monitor), `Desktop_False` (`"WorkerW"` covering monitor), `OwnWindow_False`, `NullForeground_AcceptsNotifications_False`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): fullscreen rules`.

---

### Task 5: Notification mapping, dedupe, settings

**Files:**
- Create: `src/DynamicIsland.Core/NotificationMapper.cs`, `SeenNotifications.cs`, `AppSettings.cs`
- Test: `NotificationMapperTests.cs`, `SeenNotificationsTests.cs`, `AppSettingsTests.cs`

**Interfaces:**
- Produces:
```csharp
public static class NotificationMapper
{
    public static IslandNotification? TryMap(string id, string appName, string? aumid,
        IReadOnlyList<string> texts, byte[]? logo, string? ownAumid);
}
public sealed class SeenNotifications
{
    public void Seed(IEnumerable<string> existingIds);           // ids present at startup: never shown
    public bool MarkIfNew(string id);                           // true only the first time an id is seen
}
public sealed record AppSettings(bool StartWithWindows = true, bool PlaySound = true, bool OnboardingDone = false)
{
    public static AppSettings Load(string path);   // missing/corrupt → defaults, never throws
    public void Save(string path);                 // creates directory
}
```
`ownAumid` is supplied by the caller (Task 7 reads it from `Package.Current` at runtime). Mapper: null if `aumid == ownAumid`, or all texts blank; Title = first non-blank text (trimmed); Body = remaining non-blank texts joined by `"\n"`; AppName falls back to `"Bildirim"` if blank. SeenNotifications keeps at most 500 ids (drop oldest).

- [ ] **Step 1: Write failing tests:** `Map_TitleAndBody`, `Map_SingleText_EmptyBody`, `Map_AllBlank_Null`, `Map_OwnApp_Null`, `Map_BlankAppName_Fallback`; `Seeded_IdsAreNotNew` (Review Focus 2), `SameIdTwice_OnlyFirstIsNew` (Review Focus 3), `Capacity_EvictsOldest` (501 ids → first id is new again); `Settings_Missing_Defaults`, `Settings_Corrupt_Defaults` (file contains `"{not json"`), `Settings_RoundTrip` (temp dir).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement (`System.Text.Json`). **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): notification mapping, dedupe, settings`.

---

### Task 6: Media and fullscreen adapters

**Files:**
- Create: `src/DynamicIsland/Interop/Native.cs`, `Services/MediaService.cs`, `Services/FullscreenMonitor.cs`

**Interfaces:**
- Consumes: `MediaInfo`, `FullscreenRules`, `ForegroundWindow`, `PixelRect`, `UserNotificationState`.
- Produces:
```csharp
public sealed class MediaService : IDisposable
{
    public event Action<MediaInfo?>? MediaChanged;   // raised on the thread passed to StartAsync's dispatcher
    public Task StartAsync(System.Windows.Threading.Dispatcher ui);
    public Task PlayPauseAsync(); public Task NextAsync(); public Task PreviousAsync();
}
public sealed class FullscreenMonitor : IDisposable
{
    public event Action<bool>? FullscreenChanged;    // UI thread; only on change
    public bool IsFullscreen { get; }
    public void Start();                             // must be called on UI thread (WinEvent hook needs message loop)
}
```
MediaService: `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, track `CurrentSessionChanged`; on the current session listen to `MediaPropertiesChanged`, `PlaybackInfoChanged`, `TimelinePropertiesChanged`; read thumbnail stream into `byte[]` (null on failure); `MediaInfo == null` when no session or title blank. Debounce bursts by 100 ms. All WinRT exceptions caught and logged, never thrown to callers.
FullscreenMonitor: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)` (keep delegate in a field) + 1 s `DispatcherTimer`; build `ForegroundWindow` from `GetForegroundWindow`, `GetClassName`, `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`, `IsZoomed`, `GetWindowThreadProcessId == Environment.ProcessId`; monitor via `MonitorFromWindow` + `GetMonitorInfo`; quns via `SHQueryUserNotificationState`.

- [ ] **Step 1:** Implement `Native.cs` (P/Invoke with `LibraryImport` or `DllImport`) and both services.
- [ ] **Step 2:** Temporary debug: in `App.OnStartup` log both services' events to `%LOCALAPPDATA%\DynamicIsland\debug.log`.
- [ ] **Step 3:** Run `dotnet build` → 0 errors, then run the exe from `src/DynamicIsland/bin` (no identity needed for these). Manual check with the user: play/pause Spotify → log shows title + IsPlaying flips; close Spotify → log shows `null`; open a game or F11 YouTube → `FullscreenChanged(true)`, exit → `false`; maximize a normal window → no `true`.
- [ ] **Step 4:** Remove debug logging (keep a `Log.Write` helper writing to `debug.log` only for errors). Commit `feat: media and fullscreen adapters`.

---

### Task 7: Notification adapter, app launcher, sound

**Files:**
- Create: `Services/NotificationService.cs`, `Services/AppLauncher.cs`, `Services/NotificationSound.cs`

**Interfaces:**
- Consumes: `NotificationMapper.TryMap`, `SeenNotifications`, `IslandNotification`.
- Produces:
```csharp
public enum NotificationAccess { Allowed, Denied, NoIdentity, Error }
public sealed class NotificationService : IDisposable
{
    public event Action<IslandNotification>? NotificationArrived;   // UI thread
    public Task<NotificationAccess> StartAsync(Dispatcher ui);      // never throws
    public static Task<NotificationAccess> RequestAccessAsync();    // call from UI thread (onboarding)
}
public static class AppLauncher { public static bool TryActivate(string? aumid); }  // false on failure, never throws
public sealed class NotificationSound { public bool Enabled { get; set; } public void Play(); }
```
NotificationService: check identity via `Package.Current` (catch → `NoIdentity`); `GetAccessStatus()` (no prompt) → `Denied` unless `Allowed`; seed `SeenNotifications` with current toast ids; subscribe `NotificationChanged` inside try/catch; always also poll `GetNotificationsAsync(NotificationKinds.Toast)` every 1 s on a background loop; for each `MarkIfNew` id read `AppInfo.DisplayInfo.DisplayName`, `AppInfo.AppUserModelId`, logo (`DisplayInfo.GetLogo(new Size(64,64))` → bytes, null on failure), texts from `Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric).GetTextElements()`; map with own AUMID from `Package.Current` app list entry / `AppUserModelId`; raise on UI thread.
AppLauncher: `IApplicationActivationManager.ActivateApplication(aumid, null, AO_NONE, out _)` via COM (CLSID `45BA127D-10A8-46EA-8AB7-56EA9078943C`); fallback `Process.Start("explorer.exe", "shell:AppsFolder\\" + aumid)`.
NotificationSound: `System.Media.SoundPlayer` on `C:\Windows\Media\Windows Notify Messaging.wav` (if missing, `SystemSounds.Asterisk`), no-op when `!Enabled`.

- [ ] **Step 1:** Implement the three classes.
- [ ] **Step 2:** Temporary debug log of `NotificationArrived` + access result in `App.OnStartup`.
- [ ] **Step 3:** `build.ps1`, launch. Manual check: a WhatsApp/Discord notification appears in log exactly once with title/body/app name; notifications existing before launch are not logged; `AppLauncher.TryActivate` on a logged AUMID opens that app. Then run the unpackaged exe from `bin` (no identity) → log shows `NoIdentity`, no crash (Review Focus 5b).
- [ ] **Step 4:** Remove debug logging. Commit `feat: notification adapter, launcher, sound`.

---

### Task 8: Island window

**Files:**
- Create: `Island/IslandWindow.xaml(.cs)`, `Island/MusicCompactView.xaml`, `Island/MusicExpandedView.xaml`, `Island/NotificationView.xaml`, `Island/Equalizer.xaml`

**Interfaces:**
- Consumes: `IslandStateMachine` (read `Mode`, `Media`, `CurrentNotification`; call `OnPointerEnter/Leave/OnClick`), `Spring`, `NotchGeometry.BuildPathData`, `MediaService` commands, `AppLauncher`.
- Produces:
```csharp
public partial class IslandWindow : Window
{
    public IslandWindow(IslandStateMachine sm, MediaService media, Func<DateTimeOffset> now);
    public void Render();                 // call after sm.Changed; sets spring targets + swaps content
}
```
Decisions:
- Window: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost`, `ShowInTaskbar=False`, `ShowActivated=False`, fixed size 460×140 DIPs, positioned at primary monitor top-center (`SystemParameters.PrimaryScreenWidth`), y=0. In `SourceInitialized` add `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (and `WS_EX_LAYERED` is already set by WPF).
- Click-through: while visible, a 33 ms `DispatcherTimer` reads `GetCursorPos`, converts to DIPs, hit-tests the current animated notch rect; inside → clear `WS_EX_TRANSPARENT` and call `sm.OnPointerEnter` on edge; outside → set `WS_EX_TRANSPARENT` and `sm.OnPointerLeave` on edge. Mode `Hidden` and collapse animation settled → `Hide()` the window and stop the timer; leaving Hidden → `Show()` (NOACTIVATE keeps focus) and re-assert `SetWindowPos(HWND_TOPMOST, …, SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE)`.
- Animation: three `Spring`s (width, height, contentOpacity), stepped in `CompositionTarget.Rendering` using real elapsed time; unsubscribe when all settled. Each frame: `NotchPath.Data = Geometry.Parse(NotchGeometry.BuildPathData(w, h, 10, bottomRadius))` with `bottomRadius = h<=40 ? h/2 : 22`. Hidden target = width 120, height 0 (grows "out of" the top edge). Content swaps when old content opacity reaches 0, then new content fades/scales in (ScaleTransform 0.92→1).
- Compact: 26×26 rounded cover, title (ellipsis), Equalizer (4 bars, looping storyboard; static when paused).
- Expanded: 64×64 cover, title + artist, ⏮ ⏯ ⏭ buttons (call MediaService), progress bar interpolating `Position + (now - PositionUpdatedAt)` while playing, `m:ss / m:ss` text.
- Notification: 40×40 app logo (placeholder bell glyph if null), AppName small grey, Title bold, Body max 2 lines. Click → `var n = sm.OnClick(now()); if (n != null) AppLauncher.TryActivate(n.AppUserModelId);`.
- Missing cover: dark grey rounded square with ♪ glyph.

- [ ] **Step 1:** Implement window + views.
- [ ] **Step 2:** `dotnet build` → 0 errors. Visual check comes in Task 9 (needs wiring).
- [ ] **Step 3:** Commit `feat: island window and views`.

---

### Task 9: App shell, wiring, onboarding, tray, startup

**Files:**
- Modify: `src/DynamicIsland/App.xaml(.cs)`
- Create: `Shell/TrayIcon.cs`, `Shell/StartupRegistration.cs`, `Shell/OnboardingWindow.xaml(.cs)`

**Interfaces:**
- Consumes: everything above; `AppSettings.Load/Save`.
- Produces: the finished app.

Decisions:
- Single instance: named `Mutex("DynamicIsland.SingleInstance")`; second instance exits silently.
- `App.OnStartup`: load settings → create `IslandStateMachine(IslandTimings.Default)` → `IslandWindow` → start `MediaService`, `FullscreenMonitor`, `NotificationService` → wire events to `sm.OnMediaChanged/OnNotification/OnFullscreenChanged` with `DateTimeOffset.Now`; on `NotificationArrived` also `sound.Play()` **only if** `!fullscreenMonitor.IsFullscreen`; `sm.Changed += window.Render`; a 50 ms `DispatcherTimer` calls `sm.Tick(now)` (stopped while `Mode == Hidden` and no media, restarted on any event). `ShutdownMode=OnExplicitShutdown`. Unhandled exceptions logged to `debug.log`.
- Onboarding (shown if `!OnboardingDone` or access != `Allowed` at startup): Turkish text: what the island does; button **"Bildirim erişimine izin ver"** → `NotificationService.RequestAccessAsync()` then restart the service; button **"Bildirim ayarlarını aç"** → `Process.Start("explorer.exe","ms-settings:notifications")`; instruction text per spike result (DND, or per-app "Bildirim başlıklarını göster" off); **"Tamam"** sets `OnboardingDone=true`. If access is `NoIdentity`, show "Uygulama paketli kurulmamış; build.ps1 ile kurun. Şimdilik sadece müzik çalışacak."
- Tray (`System.Windows.Forms.NotifyIcon`, app icon): menu items "Windows ile başlat" (checked = setting; toggles `StartupRegistration.Set(bool)`), "Bildirim sesi" (checked), "Kurulum rehberi" (opens onboarding), "Çıkış".
- `StartupRegistration.Set(bool)`: HKCU `Software\Microsoft\Windows\CurrentVersion\Run` value `DynamicIsland` = `"%LOCALAPPDATA%\DynamicIsland\app\DynamicIsland.exe"` (expanded path); applied on startup to match the setting.

- [ ] **Step 1:** Implement.
- [ ] **Step 2:** `dotnet test` → all Core tests PASS; `packaging/build.ps1` → registers; launch exe.
- [ ] **Step 3: End-to-end manual check with the user** (each line is pass/fail):
  1. Nothing playing, no notifications → nothing visible at top; clicking top-center of a browser hits the browser.
  2. Play Spotify → compact island with spring "pop", cover, title, moving equalizer.
  3. Hover → expands after a short beat; ⏮ ⏯ ⏭ work; progress moves; leave → shrinks.
  4. Pause → after ~5 s island disappears.
  5. Send a WhatsApp/Discord message → sound + notification view ~5 s, then back to music (or gone); no Windows banner at bottom-right (DND/banners configured).
  6. Click a notification → that app opens, island closes.
  7. Two quick messages → shown one after another.
  8. Start a fullscreen game / F11 YouTube, send a message → island never appears, no sound; exit fullscreen → message not replayed.
  9. Focus never leaves the window you were typing in when the island appears (type in Notepad while a notification arrives).
  10. Tray menu toggles work; reboot (or sign out/in) → app starts automatically.
- [ ] **Step 4:** Fix anything failing (re-run the failing line). Delete `spike/` if present. Commit `feat: wire up app shell, onboarding, tray`.
