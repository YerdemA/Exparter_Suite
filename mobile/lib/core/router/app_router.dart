import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../features/auth/presentation/forgot_password_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/register_screen.dart';
import '../../features/auth/presentation/reset_password_screen.dart';
import '../../features/auth/presentation/verify_email_screen.dart';
import '../../features/profile/presentation/profile_controller.dart';
import '../../features/profile/presentation/profile_form_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/shell/presentation/app_shell.dart';
import '../../l10n/app_localizations.dart';
import '../api/generated/export.dart';
import '../auth/session_controller.dart';
import 'app_routes.dart';

part 'app_router.g.dart';

/// Decides where a navigation may go for the given session and profile state. Returns
/// null to allow [location] as is.
String? redirectFor(AsyncValue<Account?> session, AsyncValue<Profile?> profile, String location) {
  // Email links work signed in or out, and must not be lost to the splash screen
  // when they are what launched the app.
  if (location == AppRoutes.verifyEmail || location == AppRoutes.resetPassword) {
    return null;
  }

  // Still reading the stored session: wait on the splash screen instead of flashing
  // the sign-in form at a user who is about to be signed in.
  if (session.isLoading && !session.hasValue) {
    return location == AppRoutes.splash ? null : AppRoutes.splash;
  }

  final onAuthScreen = location == AppRoutes.login ||
      location == AppRoutes.register ||
      location == AppRoutes.forgotPassword;
  if (session.value == null) {
    return onAuthScreen ? null : AppRoutes.login;
  }

  // Signed in, but not yet known whether the profile is complete. A null value is
  // what the provider held while signed out, so it counts as "not loaded yet" too.
  if (profile.value == null && !profile.hasError) {
    return location == AppRoutes.splash ? null : AppRoutes.splash;
  }

  // The app stays closed until the profile holds what matching needs. If the profile
  // could not be loaded this is skipped: the backend enforces the rule anyway, and
  // an offline user should still reach the app.
  if (profile.value case final loaded? when !loaded.isComplete) {
    return location == AppRoutes.profileSetup ? null : AppRoutes.profileSetup;
  }

  final onEntryScreen = onAuthScreen || location == AppRoutes.splash || location == AppRoutes.profileSetup;
  return onEntryScreen ? AppRoutes.home : null;
}

@Riverpod(keepAlive: true)
GoRouter appRouter(Ref ref) {
  // The router must outlive session changes, so it listens instead of watching:
  // watching would rebuild it and reset the navigation stack on every sign-in.
  final changes = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => changes.value++);
  ref.listen(ownProfileProvider, (_, _) => changes.value++);
  ref.onDispose(changes.dispose);

  final router = GoRouter(
    initialLocation: AppRoutes.splash,
    refreshListenable: changes,
    redirect: (context, state) => redirectFor(
      ref.read(sessionControllerProvider),
      ref.read(ownProfileProvider),
      state.matchedLocation,
    ),
    routes: [
      GoRoute(
        path: AppRoutes.splash,
        builder: (context, state) => const Scaffold(body: Center(child: CircularProgressIndicator())),
      ),
      GoRoute(path: AppRoutes.login, builder: (context, state) => const LoginScreen()),
      GoRoute(path: AppRoutes.register, builder: (context, state) => const RegisterScreen()),
      GoRoute(
        path: AppRoutes.forgotPassword,
        builder: (context, state) => const ForgotPasswordScreen(),
      ),
      GoRoute(
        path: AppRoutes.verifyEmail,
        builder: (context, state) =>
            VerifyEmailScreen(token: state.uri.queryParameters[AppRoutes.tokenParameter]),
      ),
      GoRoute(
        path: AppRoutes.resetPassword,
        builder: (context, state) =>
            ResetPasswordScreen(token: state.uri.queryParameters[AppRoutes.tokenParameter]),
      ),
      GoRoute(
        path: AppRoutes.profileSetup,
        builder: (context, state) => const ProfileFormScreen(isSetup: true),
      ),
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) => AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.home,
                builder: (context, state) => ComingSoonScreen(
                  title: AppLocalizations.of(context).navDiscover,
                  icon: Icons.explore_outlined,
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.matches,
                builder: (context, state) => ComingSoonScreen(
                  title: AppLocalizations.of(context).navMatches,
                  icon: Icons.handshake_outlined,
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.messages,
                builder: (context, state) => ComingSoonScreen(
                  title: AppLocalizations.of(context).navMessages,
                  icon: Icons.chat_bubble_outline,
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.profile,
                builder: (context, state) => const ProfileScreen(),
                routes: [
                  GoRoute(
                    path: AppRoutes.profileEditSegment,
                    builder: (context, state) => const ProfileFormScreen(isSetup: false),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    ],
  );
  ref.onDispose(router.dispose);
  return router;
}
