import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../features/auth/presentation/forgot_password_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/register_screen.dart';
import '../../features/auth/presentation/reset_password_screen.dart';
import '../../features/auth/presentation/verify_email_screen.dart';
import '../../features/discovery/presentation/discovery_screen.dart';
import '../../features/discovery/presentation/user_profile_screen.dart';
import '../../features/profile/presentation/profile_form_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/shell/presentation/app_shell.dart';
import '../../features/skills/presentation/skill_picker_screen.dart';
import '../../l10n/app_localizations.dart';
import '../api/generated/export.dart';
import '../auth/session_controller.dart';
import 'app_routes.dart';

part 'app_router.g.dart';

/// Decides where a navigation may go for the given session state. Returns null to
/// allow [location] as is.
///
/// An incomplete profile or unverified email does not restrict navigation: such a user
/// may browse the whole app. The restriction applies where they try to interact with
/// others, and is enforced by the backend.
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

  final onAuthScreen = location == AppRoutes.login ||
      location == AppRoutes.register ||
      location == AppRoutes.forgotPassword;
  if (session.value == null) {
    return onAuthScreen ? null : AppRoutes.login;
  }

  return (onAuthScreen || location == AppRoutes.splash) ? AppRoutes.home : null;
}

@Riverpod(keepAlive: true)
GoRouter appRouter(Ref ref) {
  // The router must outlive session changes, so it listens instead of watching:
  // watching would rebuild it and reset the navigation stack on every sign-in.
  final changes = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => changes.value++);
  ref.onDispose(changes.dispose);

  final router = GoRouter(
    initialLocation: AppRoutes.splash,
    refreshListenable: changes,
    redirect: (context, state) =>
        redirectFor(ref.read(sessionControllerProvider), state.matchedLocation),
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
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) => AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.home,
                builder: (context, state) => const DiscoveryScreen(),
                routes: [
                  GoRoute(
                    path: AppRoutes.userProfileSegment,
                    builder: (context, state) =>
                        UserProfileScreen(userId: state.pathParameters[AppRoutes.userIdParameter]!),
                  ),
                ],
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
                    builder: (context, state) => const ProfileFormScreen(),
                  ),
                  GoRoute(
                    path: AppRoutes.skillPickerSegment,
                    builder: (context, state) => SkillPickerScreen(
                      // Anything but "wanted" opens the offered picker, including a mistyped link.
                      type: state.pathParameters[AppRoutes.skillTypeParameter] == 'wanted'
                          ? UserSkillType.wanted
                          : UserSkillType.offered,
                    ),
                  ),
                  GoRoute(
                    path: AppRoutes.skillProposalsSegment,
                    builder: (context, state) => const SkillProposalsScreen(),
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
