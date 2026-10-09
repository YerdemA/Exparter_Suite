import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/router/app_router.dart';
import 'package:exparter/core/router/app_routes.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../helpers.dart';

void main() {
  const sessionLoading = AsyncLoading<Account?>();
  const signedOut = AsyncData<Account?>(null);
  final signedIn = AsyncData<Account?>(testAccount());

  const profileLoading = AsyncLoading<Profile?>();
  const noProfile = AsyncData<Profile?>(null);
  final complete = AsyncData<Profile?>(testProfile());
  final incomplete = AsyncData<Profile?>(testProfile(complete: false));
  final profileFailed = AsyncError<Profile?>(
    const ApiException(code: ApiException.networkCode),
    StackTrace.empty,
  );

  const appScreens = [AppRoutes.home, AppRoutes.matches, AppRoutes.messages, AppRoutes.profile, AppRoutes.profileEdit];
  const authScreens = [AppRoutes.login, AppRoutes.register, AppRoutes.forgotPassword];

  test('waits on the splash screen while the session loads', () {
    expect(redirectFor(sessionLoading, profileLoading, AppRoutes.home), AppRoutes.splash);
    expect(redirectFor(sessionLoading, profileLoading, AppRoutes.splash), isNull);
  });

  test('sends signed-out users to the sign-in screen', () {
    for (final location in [...appScreens, AppRoutes.splash, AppRoutes.profileSetup]) {
      expect(redirectFor(signedOut, noProfile, location), AppRoutes.login, reason: location);
    }

    for (final location in authScreens) {
      expect(redirectFor(signedOut, noProfile, location), isNull, reason: location);
    }
  });

  test('waits on the splash screen while the profile of a signed-in user loads', () {
    expect(redirectFor(signedIn, profileLoading, AppRoutes.home), AppRoutes.splash);
    expect(redirectFor(signedIn, profileLoading, AppRoutes.login), AppRoutes.splash);
    expect(redirectFor(signedIn, profileLoading, AppRoutes.splash), isNull);

    // Right after signing in the provider still holds the signed-out value.
    expect(redirectFor(signedIn, noProfile, AppRoutes.login), AppRoutes.splash);
    expect(redirectFor(signedIn, noProfile, AppRoutes.home), AppRoutes.splash);
  });

  test('keeps a user with an incomplete profile on the setup screen', () {
    for (final location in [...appScreens, ...authScreens, AppRoutes.splash]) {
      expect(redirectFor(signedIn, incomplete, location), AppRoutes.profileSetup, reason: location);
    }

    expect(redirectFor(signedIn, incomplete, AppRoutes.profileSetup), isNull);
  });

  test('lets a user with a complete profile into the app and out of the entry screens', () {
    for (final location in appScreens) {
      expect(redirectFor(signedIn, complete, location), isNull, reason: location);
    }

    for (final location in [...authScreens, AppRoutes.splash, AppRoutes.profileSetup]) {
      expect(redirectFor(signedIn, complete, location), AppRoutes.home, reason: location);
    }
  });

  test('lets a signed-in user into the app when the profile cannot be loaded', () {
    expect(redirectFor(signedIn, profileFailed, AppRoutes.splash), AppRoutes.home);
    expect(redirectFor(signedIn, profileFailed, AppRoutes.profile), isNull);
  });

  test('never redirects away from email links', () {
    for (final (session, profile) in [
      (sessionLoading, profileLoading),
      (signedOut, noProfile),
      (signedIn, profileLoading),
      (signedIn, incomplete),
      (signedIn, complete),
    ]) {
      expect(redirectFor(session, profile, AppRoutes.verifyEmail), isNull);
      expect(redirectFor(session, profile, AppRoutes.resetPassword), isNull);
    }
  });
}
