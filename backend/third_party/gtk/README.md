# GTK, GLib, GIO, gdk-pixbuf and WebKitGTK declarations

The Linux launcher loads GTK 3 and WebKitGTK 4.1 at runtime with `dlopen`, so it never links them. These headers declare only the
types, enums, macros and struct layouts that the launcher uses, so it builds without the GTK and WebKitGTK development packages.
They aren't the upstream headers. Each value and layout has to match the upstream one exactly, since a wrong value would only fail
at runtime. `abi_probe.c` checks them against the installed upstream headers.

Checked against GTK 3.24.41, GLib 2.80.0 and WebKitGTK 4.1 (2.52.6) headers, using abi_probe.c.
