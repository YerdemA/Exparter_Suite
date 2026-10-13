import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/router/app_router.dart';
import 'package:exparter/core/router/app_routes.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../helpers.dart';

void main() {
  const loading = AsyncLoading<Account?>();
  const signedOut = AsyncData<Account?>(null);
  final signedIn = AsyncData<Account?>(testAccount());

  const appScreens = [AppRoutes.home, AppRoutes.matches, AppRoutes.messages, AppRoutes.profile, AppRoutes.profileEdit];
  const authScreens = [AppRoutes.login, AppRoutes.register, AppRoutes.forgotPassword];

  test('waits on the splash screen while the session loads', () {
    expect(redirectFor(loading, AppRoutes.home), AppRoutes.splash);
    expect(redirectFor(loading, AppRoutes.splash), isNull);
  });

  test('sends signed-out users to the sign-in screen', () {
    for (final location in [...appScreens, AppRoutes.splash]) {
      expect(redirectFor(signedOut, location), AppRoutes.login, reason: location);
    }

    for (final location in authScreens) {
      expect(redirectFor(signedOut, location), isNull, reason: location);
    }
  });

  test('lets every signed-in user browse the app, whatever the state of their profile', () {
    for (final location in appScreens) {
      expect(redirectFor(signedIn, location), isNull, reason: location);
    }
  });

  test('keeps signed-in users out of the entry screens', () {
    for (final location in [...authScreens, AppRoutes.splash]) {
      expect(redirectFor(signedIn, location), AppRoutes.home, reason: location);
    }
  });

  test('never redirects away from email links', () {
    for (final session in [loading, signedOut, signedIn]) {
      expect(redirectFor(session, AppRoutes.verifyEmail), isNull);
      expect(redirectFor(session, AppRoutes.resetPassword), isNull);
    }
  });
}
