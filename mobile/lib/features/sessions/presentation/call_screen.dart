import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:livekit_client/livekit_client.dart' show VideoTrack, VideoTrackRenderer, VideoViewFit;

import '../../../core/api/error_messages.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../domain/call.dart';
import '../domain/call_encryption.dart';
import 'call_controller.dart';

/// The video call of an online session, full screen.
class CallScreen extends ConsumerStatefulWidget {
  const CallScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<CallScreen> createState() => _CallScreenState();
}

class _CallScreenState extends ConsumerState<CallScreen> {
  late final AppLifecycleListener _lifecycle;

  CallController get _controller => ref.read(callControllerProvider(widget.sessionId).notifier);

  @override
  void initState() {
    super.initState();
    _lifecycle = AppLifecycleListener(
      onHide: () => _controller.appHidden(),
      onShow: () => _controller.appShown(),
    );
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        final l10n = AppLocalizations.of(context);
        _controller.start(notificationTitle: l10n.callNotificationTitle, notificationText: l10n.callNotificationText);
      }
    });
  }

  @override
  void dispose() {
    _lifecycle.dispose();
    super.dispose();
  }

  void _back() => context.canPop() ? context.pop() : context.go(AppRoutes.home);

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final call = ref.watch(callControllerProvider(widget.sessionId));

    return Scaffold(
      backgroundColor: AppPalette.nightLowest,
      body: SafeArea(
        child: switch (call.phase) {
          CallPhase.connecting => _Message(icon: null, text: l10n.callConnecting),
          CallPhase.failed => _Message(
              icon: Icons.videocam_off_outlined,
              text: switch (call.error) {
                CallMediaException() => l10n.callMediaDenied,
                CallEncryptionException() => l10n.callEncryptionFailed,
                final error => errorMessage(l10n, error ?? Object()),
              },
              action: (l10n.callBack, _back),
            ),
          CallPhase.ended => _Message(
              icon: Icons.call_end,
              text: switch (call.end) {
                CallEnd.windowClosed => l10n.callEndedWindowClosed,
                CallEnd.left => l10n.callEndedLeft,
                _ => l10n.callEndedLost,
              },
              hint: l10n.callEndedHint,
              action: (l10n.callBack, _back),
            ),
          _ => _InCall(call: call, controller: _controller),
        },
      ),
    );
  }
}

class _InCall extends StatelessWidget {
  const _InCall({required this.call, required this.controller});

  final CallState call;
  final CallController controller;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final banner = switch (call) {
      CallState(phase: CallPhase.reconnecting) => l10n.callReconnecting,
      CallState(closingSoon: true) => l10n.callClosingSoon,
      CallState(sharingScreen: true) => l10n.callSharingScreen,
      _ => null,
    };
    final secured = call.encryption == CallEncryption.secured;
    // While the other user shares their screen, it fills the view and their camera takes
    // the small corner instead of this user's own. Their media cannot be decrypted before
    // the key is agreed, so it is not shown until then.
    final corner = secured && call.remoteScreen != null ? call.remoteVideo : (call.cameraOn ? call.localVideo : null);

    return Stack(
      children: [
        Positioned.fill(
          child: switch (call) {
            CallState(phase: CallPhase.inCall, encryption: CallEncryption.pending) =>
              _Message(icon: null, text: l10n.callSecuring),
            CallState(phase: CallPhase.inCall, encryption: CallEncryption.peerOutdated) =>
              _Message(icon: Icons.system_update_outlined, text: l10n.callPeerOutdated),
            CallState(remoteScreen: final VideoTrack screen) when secured =>
              VideoTrackRenderer(screen, fit: VideoViewFit.contain),
            CallState(remoteVideo: final VideoTrack video) when secured =>
              VideoTrackRenderer(video, fit: VideoViewFit.cover),
            CallState(phase: CallPhase.inCall) => _Message(icon: Icons.videocam_off_outlined, text: l10n.callRemoteCameraOff),
            _ => _Message(icon: Icons.hourglass_empty, text: l10n.callWaiting),
          },
        ),
        if (corner case final VideoTrack video)
          Positioned(
            top: AppSpacing.md,
            right: AppSpacing.md,
            width: AppSizes.callPreviewWidth,
            height: AppSizes.callPreviewHeight,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(AppRadius.md),
              child: VideoTrackRenderer(video, fit: VideoViewFit.cover),
            ),
          ),
        if (banner != null)
          Positioned(
            top: AppSpacing.md,
            left: AppSpacing.md,
            right: AppSpacing.md + AppSizes.callPreviewWidth + AppSpacing.sm,
            child: Material(
              color: AppPalette.nightContainerHigh,
              borderRadius: BorderRadius.circular(AppRadius.sm),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.sm),
                child: Text(banner, style: const TextStyle(color: AppPalette.nightInk)),
              ),
            ),
          ),
        if (call.safetyCode case final String code)
          Positioned(
            left: 0,
            right: 0,
            bottom: AppSpacing.lg + AppSizes.control + AppSpacing.sm + AppSpacing.md,
            child: Center(child: _SafetyCode(code: code)),
          ),
        Positioned(
          left: 0,
          right: 0,
          bottom: AppSpacing.lg,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: [
              _ControlButton(
                icon: call.microphoneOn ? Icons.mic : Icons.mic_off,
                tooltip: call.microphoneOn ? l10n.callMicrophoneOff : l10n.callMicrophoneOn,
                onPressed: controller.toggleMicrophone,
              ),
              _ControlButton(
                icon: call.cameraOn ? Icons.videocam : Icons.videocam_off,
                tooltip: call.cameraOn ? l10n.callCameraOff : l10n.callCameraOn,
                onPressed: controller.toggleCamera,
              ),
              _ControlButton(
                icon: Icons.cameraswitch_outlined,
                tooltip: l10n.callSwitchCamera,
                onPressed: call.cameraOn ? controller.switchCamera : null,
              ),
              _ControlButton(
                icon: call.sharingScreen ? Icons.stop_screen_share_outlined : Icons.screen_share_outlined,
                tooltip: call.sharingScreen ? l10n.callStopSharing : l10n.callShareScreen,
                onPressed: controller.toggleScreenShare,
              ),
              _ControlButton(
                icon: Icons.call_end,
                tooltip: l10n.callEnd,
                onPressed: controller.leave,
                danger: true,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Says the call is end-to-end encrypted, with the code both users can compare.
class _SafetyCode extends StatelessWidget {
  const _SafetyCode({required this.code});

  final String code;

  Future<void> _explain(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        icon: const Icon(Icons.lock_outline),
        title: Text(l10n.callSafetyCodeTitle(code)),
        content: Text(l10n.callSafetyCodeBody),
        actions: [TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionClose))],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Material(
      color: AppPalette.nightContainerHigh,
      borderRadius: BorderRadius.circular(AppRadius.sm),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.sm),
        onTap: () => _explain(context),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.sm),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.lock_outline, size: AppSizes.callBadgeIcon, color: AppPalette.nightInk),
              const SizedBox(width: AppSpacing.sm),
              Text(l10n.callEncrypted(code), style: const TextStyle(color: AppPalette.nightInk)),
            ],
          ),
        ),
      ),
    );
  }
}

class _ControlButton extends StatelessWidget {
  const _ControlButton({required this.icon, required this.tooltip, required this.onPressed, this.danger = false});

  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;
  final bool danger;

  @override
  Widget build(BuildContext context) => IconButton.filled(
        onPressed: onPressed,
        tooltip: tooltip,
        iconSize: AppSizes.callControlIcon,
        style: IconButton.styleFrom(
          backgroundColor: danger ? AppPalette.danger : AppPalette.nightContainerHighest,
          foregroundColor: AppPalette.white,
          minimumSize: const Size.square(AppSizes.control + AppSpacing.sm),
        ),
        icon: Icon(icon),
      );
}

class _Message extends StatelessWidget {
  const _Message({required this.icon, required this.text, this.hint, this.action});

  /// Null shows a progress indicator.
  final IconData? icon;
  final String text;
  final String? hint;
  final (String label, VoidCallback onPressed)? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon == null)
              const CircularProgressIndicator()
            else
              Icon(icon, size: AppSizes.callControlIcon * 2, color: AppPalette.nightInkMuted),
            const SizedBox(height: AppSpacing.md),
            Text(
              text,
              textAlign: TextAlign.center,
              style: theme.textTheme.titleMedium?.copyWith(color: AppPalette.nightInk),
            ),
            if (hint != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(
                hint!,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium?.copyWith(color: AppPalette.nightInkMuted),
              ),
            ],
            if (action case (final label, final onPressed)) ...[
              const SizedBox(height: AppSpacing.lg),
              OutlinedButton(
                onPressed: onPressed,
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppPalette.nightInk,
                  side: const BorderSide(color: AppPalette.nightOutline),
                ),
                child: Text(label),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
