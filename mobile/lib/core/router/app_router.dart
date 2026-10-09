import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../features/auth/presentation/forgot_password_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/register_screen.dart';
import '../../features/auth/presentation/reset_password_screen.dart';
import '../../features/auth/presentation/verify_email_screen.dart';
import '../../features/home/presentation/home_screen.dart';
import '../api/generated/export.dart';
import '../auth/session_controller.dart';
import 'app_routes.dart';

part 'app_router.g.dart';

/// Decides where a navigation may go for the given session state. Returns null to
/// allow [location] as is.
String? redirectFor(AsyncValue<Account?> session, String location) {
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

  final signedIn = session.value != null;
  final onAuthScreen = location == AppRoutes.login ||
      location == AppRoutes.register ||
      location == AppRoutes.forgotPassword;
  if (!signedIn) {
    return onAuthScreen ? null : AppRoutes.login;
  }

  return (onAuthScreen || location == AppRoutes.splash) ? AppRoutes.home : null;
}

@Riverpod(keepAlive: true)
GoRouter appRouter(Ref ref) {
  // The router must outlive session changes, so it listens instead of watching:
  // watching would rebuild it and reset the navigation stack on every sign-in.
  final sessionChanges = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => sessionChanges.value++);
  ref.onDispose(sessionChanges.dispose);

  final router = GoRouter(
    initialLocation: AppRoutes.splash,
    refreshListenable: sessionChanges,
    redirect: (context, state) =>
        redirectFor(ref.read(sessionControllerProvider), state.matchedLocation),
    routes: [
      GoRoute(
        path: AppRoutes.splash,
        builder: (context, state) => const Scaffold(body: Center(child: CircularProgressIndicator())),
      ),
      GoRoute(path: AppRoutes.login, builder: (context, state) => const LoginScreen()),
      GoRoute(path: AppRoutes.register, builder: (context, state) => const RegisterScreen()),
      GoRoute(path: AppRoutes.home, builder: (context, state) => const HomeScreen()),
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
    ],
  );
  ref.onDispose(router.dispose);
  return router;
}
