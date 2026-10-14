import '../api/generated/export.dart';

abstract final class AppRoutes {
  static const splash = '/splash';
  static const login = '/login';
  static const register = '/register';
  static const home = '/';
  static const notificationsSegment = 'notifications';
  static const notifications = '/$notificationsSegment';
  static const userProfileSegment = 'users/:$userIdParameter';
  static const userIdParameter = 'userId';
  static const sendRequestSegment = 'request';
  static const writeReviewSegment = 'review';
  static const matches = '/matches';
  static const matchDetailSegment = ':$matchIdParameter';
  static const matchIdParameter = 'matchId';
  static const proposeSessionSegment = 'sessions/new';
  static const messages = '/messages';
  static const conversationSegment = ':$conversationIdParameter';
  static const conversationIdParameter = 'conversationId';
  static const profile = '/profile';
  static const profileEditSegment = 'edit';
  static const profileEdit = '$profile/$profileEditSegment';

  static const skillPickerSegment = 'skills/add/:$skillTypeParameter';
  static const skillTypeParameter = 'type';
  static const skillProposalsSegment = 'skills/proposals';
  static const skillProposals = '$profile/$skillProposalsSegment';
  static const blockedUsersSegment = 'blocked';
  static const blockedUsers = '$profile/$blockedUsersSegment';

  static String userProfile(String userId) => '/users/$userId';

  static String sendRequest(String userId) => '/users/$userId/$sendRequestSegment';

  static String writeReview(String userId) => '/users/$userId/$writeReviewSegment';

  static String matchDetail(String matchId) => '$matches/$matchId';

  static String proposeSession(String matchId) => '$matches/$matchId/$proposeSessionSegment';

  static String conversation(String conversationId) => '$messages/$conversationId';

  static String skillPicker(UserSkillType type) => '$profile/skills/add/${type.json!.toLowerCase()}';

  // Reached from links in emails, so these paths are part of the email contract
  // (see Email:EmailVerificationUrl and Email:PasswordResetUrl on the backend).
  static const verifyEmail = '/verify-email';
  static const resetPassword = '/reset-password';

  static const forgotPassword = '/forgot-password';

  /// Query parameter carrying the one-time token in email links.
  static const tokenParameter = 'token';
}
