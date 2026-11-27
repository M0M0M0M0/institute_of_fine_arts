import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../features/notifications/notifications.dart';

/// Push notifications (Firebase Cloud Messaging) on Android and iOS.
///
/// Firebase needs the project's config file (android/app/google-services.json, ios/Runner/GoogleService-Info.plist),
/// which is kept out of git. Without it — and on the web or in tests — [start] leaves push off and the app works as
/// before; the in-app notification list does not depend on push.
///
/// Flow: after signing in the app asks for permission, gets the phone's token and registers it (POST /api/devices,
/// again on every start and when Firebase renews the token); signing out removes it (DELETE /api/devices) so the
/// next user of the phone does not get the previous user's news. A push that arrives while the app is open shows a
/// snack bar and refreshes the bell; tapping a push opens the notification's screen.
class PushController {
  PushController(this._ref);

  final Ref _ref;
  FirebaseMessaging? _messaging;
  String? _token;
  bool _signedIn = false;
  final _subscriptions = <StreamSubscription<Object?>>[];

  /// True once Firebase is ready on this phone.
  bool get isEnabled => _messaging != null;

  /// Starts Firebase; [onMessage] shows a push that arrives while the app is open, [onOpen] opens the screen of a
  /// tapped push (also the one that launched the app). Never throws.
  Future<void> start({
    required void Function(String title, String body, String? link) onMessage,
    required void Function(String? link) onOpen,
  }) async {
    if (kIsWeb || _messaging != null) return;
    try {
      await Firebase.initializeApp();
    } catch (e) {
      debugPrint('Push notifications are off: Firebase is not configured ($e)');
      return;
    }
    final messaging = FirebaseMessaging.instance;
    _messaging = messaging;

    _subscriptions.add(FirebaseMessaging.onMessage.listen((m) {
      _ref.invalidate(unreadCountProvider);
      _ref.invalidate(notificationListProvider);
      final n = m.notification;
      if (n != null) onMessage(n.title ?? 'Notification', n.body ?? '', m.data['link'] as String?);
    }));
    _subscriptions.add(FirebaseMessaging.onMessageOpenedApp.listen((m) => onOpen(m.data['link'] as String?)));
    _subscriptions.add(messaging.onTokenRefresh.listen((token) {
      _token = token;
      if (_signedIn) _send(token);
    }));

    final launch = await messaging.getInitialMessage();
    if (launch != null) onOpen(launch.data['link'] as String?);
    if (_signedIn) await signedIn();
  }

  /// The user is signed in (after login or when the app restores the session): register this phone.
  Future<void> signedIn() async {
    _signedIn = true;
    final messaging = _messaging;
    if (messaging == null) return;
    try {
      final settings = await messaging.requestPermission();
      if (settings.authorizationStatus == AuthorizationStatus.denied) return;
      final token = await messaging.getToken();
      if (token == null) return;
      _token = token;
      await _send(token);
    } catch (e) {
      debugPrint('Push registration failed: $e');
    }
  }

  /// Called just before signing out, while the session still works: this phone stops getting pushes.
  Future<void> signingOut() async {
    _signedIn = false;
    final token = _token;
    if (_messaging == null || token == null) return;
    try {
      await _ref.read(notificationRepositoryProvider).unregisterDevice(token);
    } catch (e) {
      debugPrint('Push unregistration failed: $e');
    }
  }

  /// The session ended without a chance to tell the server (token refused, account locked): delete this phone's
  /// push token at Firebase, so whatever the server still sends to it is not delivered.
  Future<void> sessionEnded() async {
    _signedIn = false;
    _token = null;
    final messaging = _messaging;
    if (messaging == null) return;
    try {
      await messaging.deleteToken();
    } catch (e) {
      debugPrint('Push token deletion failed: $e');
    }
  }

  Future<void> _send(String token) async {
    try {
      final platform = defaultTargetPlatform == TargetPlatform.iOS ? 'ios' : 'android';
      await _ref.read(notificationRepositoryProvider).registerDevice(token, platform);
    } catch (e) {
      debugPrint('Push registration failed: $e');
    }
  }

  void dispose() {
    for (final s in _subscriptions) {
      s.cancel();
    }
  }
}

final pushProvider = Provider<PushController>((ref) {
  final controller = PushController(ref);
  ref.onDispose(controller.dispose);
  return controller;
});
