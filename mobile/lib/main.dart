import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    // Configured from google-services.json at build time.
    await Firebase.initializeApp();
  } on Object {
    // A build without a Firebase project still runs; it only gets no pushes.
  }

  runApp(const ProviderScope(child: ExparterApp()));
}
