import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sentry_flutter/sentry_flutter.dart';

import 'app.dart';
import 'core/config/app_config.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    // Configured from google-services.json at build time.
    await Firebase.initializeApp();
  } on Object {
    // A build without a Firebase project still runs; it only gets no pushes.
  }

  void run() => runApp(const ProviderScope(child: ExparterApp()));
  if (AppConfig.sentryDsn.isEmpty) {
    run();
    return;
  }

  // Uncaught errors are reported with the device and app version, and of the user only
  // their id (errorReportUserProvider); no personal data, no performance tracing.
  await SentryFlutter.init(
    (options) => options
      ..dsn = AppConfig.sentryDsn
      ..sendDefaultPii = false
      ..tracesSampleRate = 0,
    appRunner: run,
  );
}
