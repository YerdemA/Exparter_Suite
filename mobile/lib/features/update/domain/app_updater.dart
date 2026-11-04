/// What the store has for this installation.
enum UpdateOffer {
  none,

  /// A newer version can be downloaded while the app keeps running.
  available,

  /// A newer version was downloaded earlier and only waits to be installed.
  downloaded,
}

/// Newer versions from the store the app was installed from. Testers seldom open the
/// store, and automatic updates are often off or wait for Wi-Fi, so the app offers them.
abstract interface class AppUpdater {
  /// [UpdateOffer.none] also when the store cannot be asked, for instance because the
  /// app was not installed from it.
  Future<UpdateOffer> check();

  /// Shows the store's own prompt and, if the user agrees, downloads in the background.
  /// True once the download is complete; false if the user declined or it failed.
  Future<bool> download();

  /// Installs the downloaded version and restarts the app.
  Future<void> install();
}
