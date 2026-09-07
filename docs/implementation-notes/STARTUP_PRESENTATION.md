# Startup presentation fix - 0.1.1

The owner reported that the inherited engine interface appeared before the
Mesh Me360 shell. The staged document previously remained visible while its
asynchronous modules loaded. WPF's startup overlay cannot reliably occlude a
WebView2 native child window.

The native WebView now starts Hidden (retaining layout) and is revealed after
the engine/shell handshake. The staged HTML has a parser-time visibility guard;
bootstrap releases it only after shell construction and native startup succeed.
Failure keeps the existing native error surface. No geometry algorithms changed.

Staging uses Mesh Me360's original icon and product name for the inherited page
presentation. The pinned upstream tree, compatibility filenames/storage keys,
original copyright and license attribution remain intact. Legal notices and
About retain the required original-engine credits.

Validation: solution build passed with zero warnings/errors; delayed module
startup recorded 14 waiting frames and zero legacy-UI leaks, followed by a
visible Mesh Me360 shell and successful cube import. Hidden WebView initialization
passed. Native Help and About (both themes) passed. Offline asset checks verify
the staged logo against Mesh Me360's asset while retaining all engine baseline
checks. Version 0.1.1 enables an MSI upgrade from 0.1.0. Physical Windows release
acceptance remains a separate gate.

Packaged verification: MSI tables passed; all 1,264 administratively extracted files matched the build hashes. The extracted app/fixed runtime recorded seven delayed-start waiting frames and zero leaks, then loaded the model successfully. A separate native hidden-through-handshake test and Help command passed. All six offline and 38 Core checks passed. MSI SHA256: 54eabad33e9dd20a6ed3e8f21a64e3ca2ad34c3ee2e46f3b83036deabb0f4162.
