import 'package:dio/dio.dart';

/// A failed API call, reduced to what the UI needs to choose a message.
class ApiException implements Exception {
  const ApiException({
    required this.code,
    this.statusCode,
    this.fieldErrors = const {},
  });

  /// Maps a [DioException] using the `code` of the backend's problem details.
  factory ApiException.fromDio(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const ApiException(code: networkCode);
    }

    final data = response.data;
    if (data is Map<String, Object?> && data['code'] is String) {
      final errors = data['errors'];
      return ApiException(
        code: data['code']! as String,
        statusCode: response.statusCode,
        fieldErrors: errors is Map<String, Object?>
            ? {
                for (final MapEntry(:key, :value) in errors.entries)
                  if (value is List) key: value.whereType<String>().toList(),
              }
            : const {},
      );
    }

    return ApiException(code: unknownCode, statusCode: response.statusCode);
  }

  static const networkCode = 'network_error';
  static const unknownCode = 'unknown_error';
  static const sessionExpiredCode = 'session_expired';

  /// The backend's stable error code, or one of the client-side codes above.
  final String code;
  final int? statusCode;

  /// Field name to messages, for `validation_failed`. The messages are in English and
  /// meant for developers; the UI only uses the field names.
  final Map<String, List<String>> fieldErrors;

  @override
  String toString() => 'ApiException($code, status: $statusCode)';
}

/// Runs an API call and converts transport errors into [ApiException].
Future<T> guardApiCall<T>(Future<T> Function() call) async {
  try {
    return await call();
  } on DioException catch (exception) {
    throw ApiException.fromDio(exception);
  }
}
