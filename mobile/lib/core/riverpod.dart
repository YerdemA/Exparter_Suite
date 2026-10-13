/// Riverpod retries a failed provider by default. Providers that call the API opt out
/// with `@Riverpod(retry: noRetry)`: a rejected request stays rejected, and screens
/// offer an explicit "try again" instead of silent background retries.
Duration? noRetry(int retryCount, Object error) => null;
