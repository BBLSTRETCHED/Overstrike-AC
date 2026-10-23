# Overstrike AC — experimental Arkham City fork

**Current build:** Install ZIP now installs and registers included suitcode automatically. See “Automatic ZIP installation” at the end. Earlier step-by-step confirmation notes describe older builds.

This is a **local source fork of Tkachov/Overstrike**, not a fork published to a GitHub account and not an official upstream release. See `UPSTREAM_COMMIT.txt` for the exact parent revision. The upstream source, license and original application remain present. Default startup now opens an Arkham City-specific WPF window; `Overstrike AC.exe --original` follows upstream startup instead.

## Windows build supplied separately
Extract the entire Windows ZIP, keeping `ArkhamCity/logo.png` beside the executable in its folder. Run **Overstrike AC.exe**. This is a self-contained Windows x64 .NET build: Python and a separately installed .NET runtime are not required. It is unsigned and experimental. Built on Linux with Windows targeting; Windows execution, appearance and actual game compatibility have **not** been verified. The Linux cross-publish reported that Windows executable resources were not customized; the application icon may therefore be generic. The original upstream target (.NET 7) is retained, which is out of support; update the framework and audit dependencies before a public production release.

On first setup, Browse asks for `Binaries/Win32/BatmanAC.exe` inside the GOTY installation. Existing Python-build state is read automatically from `%LOCALAPPDATA%\ArkhamCityManager\state.json`. Native code uses the same library and backups and preserves extra top-level state. Back up that entire application-data folder before trying this fork, close the game, and never run the Python and native managers together. Native portrait assignments use a separate `native-portraits.json`; previous Python portrait selections are not migrated.

## Implemented here
- Native C#/WPF Arkham City window, red/charcoal/silver theme and user-supplied logo.
- Compact searchable/sortable Mods list: imported ZIP rows and read-only discovered suit registrations.
- Stage checkbox selections, Check/Uncheck all (managed visible rows), Revert pending selections and Apply.
- ZIP imports mapping CookedPCConsole and Localization into BmGame. Nested archive wrappers supported. Screenshots and optional `voice lines` folders skipped. Executable payloads rejected; no script loader installation. Repackage RAR/7z as ZIP first, one variant per archive.
- Managed file conflict checks, original-file backups, restore and added-file removal. External modification checks refuse overwriting modified managed files.
- Read-only PlayableCharactersV2 discovery from INI/TXT/CFG under BmGame/Config, with UTF-16 BOM support. Scans run at startup, after game selection, on request and after Apply.
- Suit-card grid with character/search filters, source/ID detail, local portrait assignment and scan report. Enabled managed archives get suitcode-derived cards or a single payload fallback card.
- Launch game, remove disabled managed archives, manual suitcode viewer.

## Important limits and safety
A config entry is not proof that its packages exist, nor that it is a third-party mod. Unknown package files in the scan report may be base-game files. They are not automatically adopted, disabled or deleted. Already-installed entries are displayed read-only because there is no authoritative file mapping or original backup. Duplicate detected names are suppressed only when active managed ZIP suitcode matches them. This does not fully resolve archive-to-suit identity.

No automatic suit registration, ID allocation, suit ordering/presets, embedded game portraits, drag-and-drop, Nexus metadata, automatic downloads or updates. The upstream Insomniac asset/TOC installer is deliberately not used for UE3 files. This is a separate Arkham City window/backend within the fork, not an Arkham City adapter for every original Overstrike subsystem.

Batch Apply is not atomic. If one operation fails, prior successful operations remain recorded. Per-file backup records are saved before game writes, but full power-loss transaction recovery is not guaranteed. Use independent backups. Original backups may contain modifications made before the manager was used. Do not delete app data while mods are enabled. Never use the original game installers on Arkham City.

## Build source on Windows
Install the .NET 7 SDK or Visual Studio 2022 with .NET desktop development and .NET 7 targeting support. In PowerShell run:

```powershell
.\Build-ArkhamCity.ps1
```

Output is `artifacts\ArkhamCity`. The checked-in GitHub Actions workflow can build a Windows artifact manually. Source includes the portable test harness:

```powershell
dotnet run --project CityStore.Tests/CityStore.Tests.csproj -c Release
```

## Validation performed
The full original Overstrike project with fork additions compiled with **0 errors** (many existing nullable warnings and dependency warnings remain). Windows x64 single-file/self-contained publish succeeded. Portable C# tests passed for ZIP mapping, install, managed suit fallback, conflicts, external edits, backup restoration, added-file removal, UTF-16 scanning, unknown package reporting, traversal rejection and state persistence. No Windows UI or in-game test was possible here.

## Publish as your own GitHub fork
1. Open https://github.com/Tkachov/Overstrike and click **Fork** on GitHub.
2. Clone your fork to your PC.
3. Extract this source archive elsewhere and copy its contents into your cloned fork, preserving the clone's `.git` directory. Review changes with `git diff`.
4. Create a branch and commit/push:
   ```powershell
   git switch -c arkham-city
   git add .
   git commit -m "Add experimental Arkham City WPF manager"
   git push -u origin arkham-city
   ```
5. On GitHub run the **Build Arkham City fork** workflow from that branch to obtain a Windows artifact. Review and test before publishing releases.

The downloaded source archive is a source snapshot; it does not contain GitHub authentication or automatically create a hosted fork.

## License and credits
Original Overstrike by Tkachov and contributors. Upstream source and this derivative code are distributed under GPLv3; retain `LICENSE.txt`, notices and corresponding source when redistributing. The official Batman Arkham City logo was provided by the user; it is third-party trademark/artwork, not claimed to be GPL-licensed. No game data or mod archives are bundled. Review rights to distribute the logo before public release; remove it if needed (the UI falls back to text). Other upstream third-party resources remain subject to their respective rights.

## Expanded discovery update: beyond suits
The scanner now inventories **all readable regular files under the selected game root**, not just suit entries or CookedPCConsole packages. Categories include packages, texture caches, configs, binary/ASI files, scripts, audio, localization and other files. Classification is by file type, not a claim about mod identity. Scanning never executes binaries/scripts or invokes mod installers.

### How to use
1. Open **Settings → Show unverified files in Mods** to see the broad inventory. Original game files will be included, explicitly labeled **Unverified file**; they are hidden by default to avoid misrepresenting them as mods.
2. For reliable change detection, use **Build baseline from separate clean game**. Select BatmanAC.exe from a separate, unmodified installation of the same GOTY edition/build. This folder is read and hashed, not altered. After comparing, **Added vs clean**, **Changed vs clean**, and **Missing vs clean** entries appear in the main Mods list. Updates, settings and utilities can cause differences too; these entries are not named mod identifications. The current selected game cannot be used as its own clean baseline.
3. To identify a direct-file mod by name, choose **Add mod ZIP fingerprint (read-only)**. Select a ZIP containing one variant, give it a name, and confirm the destination for any loose files. Explicit BmGame/Binaries/Engine or CookedPCConsole/Localization/Config folders map automatically. For a loose gameplay UPK, use BmGame/CookedPCConsole only if that matches its README. The tool hashes file contents and compares them with installed destinations; it does not install the archive. **Archive files match** means all mapped file bytes match; **Partial match · review** does not prove the mod is fully installed. References remain visible when absent so you can inspect missing/different counts by double-clicking their row.
4. Use **View scan report** in Suits Menu for detailed evidence and warnings. Double-click any non-suit detection row to view its evidence. **Clear detection references and baseline** removes only local comparison metadata, never game files or managed backups.

Large games can take several minutes to hash. Comparison work runs in the background; controls are disabled during it. Inaccessible directories and linked/junction paths are skipped. Scope is the game root only: external Documents/AppData config, TexMod sessions, registry and memory-only changes are not scanned. There is no universal known-mod signature database. Replacement files cannot be identified as a particular mod from filenames alone. Matched references confirm file bytes, not who installed them, load order or runtime compatibility. All discoveries remain read-only; missing original backups are not invented. Existing managed ZIP installation scope has NOT expanded: only CookedPCConsole/Localization direct-file payloads are supported for installation.

### The two requested examples
- **Arkham City Community Patch**: https://www.nexusmods.com/batmanarkhamcity/mods/1 — its public instructions say to use TFC Installer. This build does not implement TFC Installer's patch/merge operations or claim to recognize every Community Patch version. A clean baseline can reveal its resulting changes. A direct-file ZIP fingerprint works only if it actually contains the final installed file bytes, not patch recipes or a setup executable. Continue using its author's installer.
- **Faster Gliding and Special Combo Gadgets**: https://www.nexusmods.com/batmanarkhamcity/mods/749 — the public page directs users to an archive README and credits UPK Explorer. A replacement package can be found by clean comparison and named through a matching direct-file ZIP reference. The actual archive/README was not available for validation here, so this is NOT a claim of tested compatibility with that release.

No files from either Nexus mod are bundled, and no unverified signatures or hard-coded version names are presented as installed. The expanded portable suite passed 19 checks including gameplay replacements, binary additions, missing config, clean-baseline refusal for the current game, full reference matches and partial-match labeling. Full WPF build/publish was repeated; Windows UI and actual game/mod testing remain pending.

## Arkham City Remastered discovery profile (Nexus 1202)
Public instructions reviewed: https://www.nexusmods.com/batmanarkhamcity/mods/1202

The author lists ShaderFixes, d3d11.dll, d3dcompiler_46.dll and d3dx.ini under Binaries/Win32, and BmInput.ini under Documents/WB Games/Batman Arkham City GOTY/BmGame/Config. They require DirectX 11, Depth of Field ON and Bloom ON. Launcher setting changes may regenerate BmInput.ini and remove the snow configuration. Follow the author's README for your exact release; this manager does not change settings or install these hooks.

**Settings → Add Arkham City Remastered ZIP fingerprint** creates a read-only reference with separate binary and external-config destinations. ShaderFixes files, including text-based shader replacements, are categorized as SHADER. Recognized loose binary components map to Binaries/Win32; BmInput.ini maps to the virtual reference key @UserConfig/BmInput.ini. Re-zip a single extracted variant if needed. No mod download, DLL load, script execution or file installation occurs. No authored mod files or precomputed authoritative hashes are bundled.

**Settings → Select Documents Config folder to scan** asks you to pick BmInput.ini and explicitly approve read-only access to INI/CFG files directly in its folder. No other Documents folders are searched. The selection is saved locally in user-config-folder.json. **Stop scanning Documents Config** removes the selection. Unverified user configs are hidden unless Show unverified files is checked; matched configs contribute to the named ZIP reference result. Full fingerprint matches require every mapped component, including external configuration. Without the folder selection those external files are counted missing, not assumed present. Modified launcher settings may make a validly customized installation differ from the original archive.

The generic **Shader injection components** finding merely reports observed ShaderFixes/d3dx.ini/d3d11.dll components; it deliberately does not claim they uniquely identify Remastered, work together, or load correctly. Do not treat DLL presence as proof of a particular mod. Full named identification requires your actual archive fingerprint and remains a byte comparison, not runtime verification.

Validation: all **26 synthetic C# checks passed**, including profile mapping, text-shader classification, external-config opt-in/opt-out, byte matching across both locations and confirming no writes to Documents config. WPF Windows x64 publish succeeded again. The real Nexus archive and Windows/game behavior have not been tested here. Existing installation scope remains CookedPCConsole/Localization only; install Remastered manually per its author.

## Readability and Settings redesign
Added a shared CityTheme.xaml resource dictionary with explicit light TextBlock colors, dark input fields, custom dark dropdowns and tabs, visible keyboard focus, and high-contrast hover/selection states. This fixes the black-on-dark Settings descriptions caused by WPF defaults. Base text is 14 DIP; mod rows are 32 DIP tall. Settings uses four explanatory cards, wrapped descriptions, bounded action buttons and a scrollable page rather than full-width button strips. A smaller banner leaves more room for content. Scanner behavior and existing library data are unchanged.

Calculated principal text/background contrast ratios: body 14.15:1, description 9.14:1, button 13.55:1, selected-row text 8.74:1. These are color-pair checks, not a full accessibility or rendered-UI audit. The WPF project compiled/published successfully after the theme change, and the 26 backend checks still pass. Windows visual testing remains pending.


## Application branding
The current application is named **Overstrike AC** in its main window, product/title metadata and distributed executable (`Overstrike AC.exe`). Internal assemblies and namespaces remain Overstrike for upstream resource compatibility. The historical `%LOCALAPPDATA%\ArkhamCityManager` data path is deliberately unchanged so libraries and backups remain available. This is still an experimental, unofficial derivative; original credits and GPL notices are retained.

## Custom application icon
The user-supplied red/black PNG was converted to a square ICO with 16, 24, 32, 48, 64, 128 and 256 pixel entries. Its white background is preserved. The project ApplicationIcon now references ArkhamCity/OverstrikeAC.ico and the WPF main window explicitly loads that embedded icon. The Batman Arkham City banner is unchanged.

The latest prebuilt Windows package is a **self-contained multi-file build**. Extract the ENTIRE folder and keep all DLLs/runtime files beside `Overstrike AC.exe`; copying only the EXE will not work. No Python or separate .NET installation is needed. A copy of `Overstrike AC.ico` is provided for shortcut use. Windows-source builds via the existing PowerShell script still produce the configured single-file build and embed ApplicationIcon normally.

Because .NET's Linux cross-publisher skips native executable resource customization, the prebuilt unbundled apphost was given RT_ICON/RT_GROUP_ICON resources using the included tools/embed_windows_icon.py helper (LIEF). The resulting PE was reparsed and all seven embedded sizes verified. Do not use that helper on a bundled single-file executable. Native icon resources and WPF compilation were checked; Windows Explorer/taskbar behavior has not been tested here. Windows may cache the old icon: try running the new EXE from a fresh extraction folder and recreating any old pinned shortcut.

## Clean distribution layout — current packaging
The current download supersedes the previous multi-file packaging. Its top level contains only:

```
Overstrike AC/
  Overstrike AC.exe
  Support/
```

The EXE is a compressed, self-contained Windows x64 single-file bundle. Support contains the banner asset, license, documentation and upstream revision. No DLLs or debug-symbol files are loose at the top level. Keep Support beside the EXE for the banner and documentation. No Python or separate .NET install is required. .NET may extract bundled native runtime components into its own temporary cache when launched; it does not scatter them in the application folder.

Library, state, managed backups and portrait data remain at `%LOCALAPPDATA%\ArkhamCityManager`, preserving compatibility. This is a clean application distribution, not a migration to portable data folders. No fake empty Mods Library/Profiles folders are created.

The Linux cross-build now embeds the icon into the unbundled single-file host **before** .NET generates the final bundle, preserving native resource data and letting .NET compute correct bundle offsets. Use `-p:PatchCityIcon=true` on Linux with Python/LIEF installed. Normal Windows builds embed the configured ApplicationIcon automatically. Validation found 7 final PE icon sizes and a version 6 bundle header containing 461 entries with an in-range header offset; 26 portable backend checks still pass. Windows startup/Explorer visual verification remains pending.

## Managed clean installation and visual polish
**Clean installation…** is available in Mods and Settings. It is explicitly a **managed-mod restore**, not Steam validation, a pristine reinstall, or removal of every detected mod. A confirmation lists the enabled managed-mod/file counts and explains the limits. A running BatmanAC process blocks the action. Pending checkbox selections are discarded on a confirmed restore.

Before any restoration, all recorded targets are checked for external modifications and every required original backup is checked for existence/readability. A preflight failure performs no restore. With successful checks, managed replacements are restored from their backups and managed additions removed, using the existing journal. Imported archives and backups are retained; managed mods become disabled. Manually installed mods, saves, external config and manually added suit registration entries remain untouched. Backups may already contain pre-manager modifications, so this does NOT guarantee vanilla game files.

Preflight is not an atomic transaction: another program, a file lock, disk error or shutdown after preflight can interrupt restoration. Earlier completed changes remain recorded. Close the game and other mod tools and keep independent backups. Resolve external-edit/missing-backup failures manually rather than forcing deletion.

The UI keeps the existing readable charcoal/red theme, with sharper button corners, a clearer red Apply action, compact column headers, and a dedicated full-width maintenance card in Settings. The clean distribution remains just the EXE and Support folder.

All **35 portable checks passed**, including new bulk-restoration, all-mod preflight, missing-backup refusal, external-edit refusal, library/backup retention, managed-added-file removal, and save/manual-mod/user-config preservation tests. The updated WPF build published successfully with the custom icon embedded before bundling. Native Windows visual/game testing remains pending.

## Managed controls clarification and bulk selection fix
Detected-only rows now display a diamond marker rather than disabled checked boxes. A visible explanation distinguishes detection from managed installation. The toolbar counts visible managed vs read-only rows, and the bulk button says Check managed / Uncheck managed (or No managed ZIPs). Clicking it without managed rows opens an explanation instead of silently doing nothing. Apply/Revert are enabled only when real managed checkbox changes are pending, and Apply shows the pending count. Checkbox clicks update the control state immediately; double-clicking a checkbox no longer also invokes the row's double-click toggle.

The former Clean installation button is now labeled **Restore managed mods…** everywhere. With no enabled managed mods it opens an explanation of detection/backups and safe next steps, not a misleading clean-install action. It still cannot remove manual installations without trustworthy originals. Importing a ZIP over already-modded files does not create vanilla backups; restore known-clean originals before relying on backups for future installations.

Portable bulk-selection tests now cover empty managed lists, check all, uncheck all, preservation of filtered-out selections and staged-vs-installed state separation. All **40 portable checks** passed; WPF publish succeeded. The desktop interaction changes still require Windows testing.

## Current suit installation flow — fixes imported-but-not-installed ZIPs
**Import ZIP** now explicitly offers **Install now**. Choosing No imports only; it does not claim anything was installed. Checking a managed ZIP and using Apply remains supported. Payload installation is followed by a separate confirmed suit-registration offer when usable suitcode was included in the archive. Existing imported or enabled ZIPs can use **Register suits…** in Mods (also available in Settings/Suits Menu for the selected managed ZIP). Rows with suitcode but no completed registration are labeled **Files only · register suits**.

Registration scans for INI/CFG/TXT candidates beneath the game's BmGame/Config containing PlayableCharactersV2 entries. A candidate is not proof of the active game config. You must explicitly confirm the correct file; multiple candidates are not automatically selected. Browse can choose another existing file under that folder, but files without existing suit entries are rejected rather than guessing a section. No external Documents config is edited by this feature.

For entries such as the Wesker Batman_MidnightWesker and Nightwing_MidnightWesker definitions, the manager replaces placeholder/numeric IDs with fresh positive IDs starting at least at 100 and above the numeric IDs found across readable suit config candidates. It preserves BaseId and other supplied fields. Duplicate names already in the selected file are skipped; existing entries are never adopted for removal. This does not prove game-internal or dependency-reserved IDs are free; review the displayed lines and follow the mod author's setup requirements.

A second confirmation shows the exact destination and planned lines. A complete pre-edit config backup is saved beneath `%LOCALAPPDATA%\ArkhamCityManager\config-backups\<mod-id>`. New lines are inserted immediately after the last existing suit entry, not blindly at EOF, preserving its INI section. UTF-8/UTF-16 BOMs and existing line endings are preserved; byte-preserving Latin-1 fallback handles non-UTF8 legacy content. State tracks only manager-added lines. Disable/Restore managed removes only those exact lines, preserving unrelated settings and manual suits. If a tracked entry was externally edited, removal refuses instead of guessing. Pre-existing registrations are left alone. Every actual config removal also gets a pre-edit backup.

Operations are still not fully atomic across payload/config/state writes. If registration is skipped or fails after payload installation, the files stay installed and the row indicates registration is needed. Select its managed ZIP and retry Register suits… after resolving the issue. Close the game and config editors before changes and keep independent backups.

The real Nexus archive was not available for testing. The update was tested with synthetic Wesker-format suitcode and file payloads, not distributed mod assets. It does not implement missing dependencies or guarantee that the selected file is consumed by the game. Existing Community Patch/custom suit-menu requirements remain the user's responsibility.

Validation: **52 portable checks passed**, including new placeholder ID assignment, cross-config ID collision avoidance, BaseId preservation, two-character registration, correct INI section placement, UTF-16/CRLF preservation, full config backup, repeat-registration idempotence, edited-entry refusal and selective removal preserving later user settings. WPF publish and final icon/bundle validation succeeded; Windows UI and in-game testing remain pending.

## Automatic ZIP installation — current default
This supersedes the previous per-mod confirmation workflow. The main action is now **Install ZIP**: select an archive and Overstrike AC imports it, installs its supported payload and registers usable included suitcode automatically. There is no extra Install now, Apply or per-mod registration confirmation in this flow. The game must be closed and the game root configured first. The existing checkbox/Apply workflow still handles re-enabling library entries, and now also registers suits automatically.

Config selection: a single existing suit-entry config candidate is used automatically. With several candidates, a **one-time setup** asks which is active; its choice is saved per game root in auto-suit-config.json. No manual code/ID editing is required. **Settings → Choose automatic suit config…** can change it later. Previously confirmed registration also saves this preference. A stale saved file causes an error rather than silently selecting a different file. If no suitable file exists, installation stops before writing the payload; setup/dependencies need to be resolved first. If setup is cancelled, the imported archive remains inactive in the library.

Automatic registration retains backups, encoding preservation, same-section insertion, existing-name skipping, unique numeric ID allocation and manager-owned-line removal. An automatic install that fails registration attempts to restore its newly installed payload. A rollback failure is reported explicitly; this remains a best-effort multi-file operation, not an atomic transaction. Existing enabled legacy payloads are not uninstalled merely because a registration retry fails.

**Repair suits…** retries automatic registration for an already-enabled ZIP from an older version. It is not required for newly installed ZIPs. Archives without usable suitcode can install supported files, but the manager cannot invent character definitions or dependencies from filenames. The actual Wesker archive and game behavior remain unverified here.

All **63 portable checks passed**, including remembered vs unique config selection, stopping ambiguous installs before writes, automatic registration, pre-existing-name preservation, invalid-code preflight and rollback of a payload when a config-backup failure is simulated. The WPF single-file build published successfully. Native Windows/in-game testing is still pending.

## Final recheck and console-window fix
See VALIDATION_REPORT.md for the latest 71-check regression summary and explicit unverified areas. The unwanted console was traced to the prebuilt host's WINDOWS_CUI subsystem despite the WinExe project setting. The cross-build helper now sets WINDOWS_GUI before the final single-file bundle is generated. Final PE subsystem, seven icons, bundle manifest and all 461 decompressed payloads were verified. A standard uninstall now preflights all required backup files, and the main window cannot close during a background operation. Windows/in-game execution remains untested here; no claim of universal compatibility is made.


## Button verification tooling
See BUTTON_AUDIT.md. The Windows-only CityUi.Tests project and Test-OverstrikeAC-UI.ps1 script exercise actual WPF Click handlers against temporary fixtures with scripted native dialogs/process launches. GitHub Actions is configured to run it before publishing on Windows. It compiled here but has not been run on Windows. The current build has 72 passing portable checks; do not interpret compilation or authored test cases as completed Windows interaction verification.
