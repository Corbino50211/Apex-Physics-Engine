# Apex Package Updates

Apex Physics Engine 0.3.4 adds an editor updater for Git-installed package releases.

## Open the updater

Choose:

```text
Apex Physics Engine
→ Updates
→ Check for Updates...
```

The updater displays the installed package version, the newest version on the PancakeDevs release branch, and the current update status.

## Manual update

1. Save open scenes and scripts.
2. Open the updater.
3. Click **Check for Updates**.
4. When a newer version is found, click **Update Now**.
5. Unity Package Manager installs the exact newest commit and reloads scripts normally.

The updater pins the dependency to the exact detected commit so Unity does not reuse an older Git package cache entry.

## Automatic settings

- **Check for updates when Unity opens** is enabled by default. Apex checks at most once every 12 hours.
- **Install a newer version automatically after checking** is disabled by default.

Automatic installation can trigger a script reload, so save open work before enabling it.

## Recovery actions

- **Reinstall Latest Revision** installs the newest known commit again even when the semantic version already matches.
- **Copy Package URL** copies the exact Git package URL for manual Package Manager installation.

## First updater installation

Version 0.3.4 must be installed through the normal Unity Package Manager Git URL. Once 0.3.4 or newer is present, later Apex versions can be checked and installed from the updater window.

## Network behavior

The updater reads the public PancakeDevs GitHub package manifest and release-branch revision. It does not request or store a GitHub token. A working internet connection and Git access are required for installation.
