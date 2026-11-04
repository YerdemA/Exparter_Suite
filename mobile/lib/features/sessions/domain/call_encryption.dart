import 'dart:convert';

import 'package:cryptography/cryptography.dart';
import 'package:flutter/foundation.dart';

/// The topic of the data messages that carry a device's public key, kept apart from any
/// other data a room might carry later.
const callKeyTopic = 'exparter.call-key';

/// The call's encryption could not be set up; the call is not held without it (ADR 0016).
class CallEncryptionException implements Exception {
  const CallEncryptionException();
}

/// What the two devices of a call agreed on.
@immutable
class CallSecret {
  const CallSecret({required this.key, required this.safetyCode});

  /// The 32 bytes with which the media is encrypted. Never leaves the device.
  final Uint8List key;

  /// The same six digits on both devices, as "482 913", unless something stands between
  /// them and swapped the public keys.
  final String safetyCode;
}

/// One device's side of agreeing on a call's key: an X25519 key pair made for this call
/// alone, whose public half goes to the other device through the media server. The
/// server sees only public keys, so it cannot work out the key both devices arrive at.
class CallKeyAgreement {
  CallKeyAgreement._(this._keyPair, this.publicKey);

  static const _version = 1;
  static const _keyLength = 32;
  static final _x25519 = X25519();
  static final _hkdf = Hkdf(hmac: Hmac.sha256(), outputLength: _keyLength);
  static final _info = utf8.encode('exparter call e2ee v1');

  final SimpleKeyPair _keyPair;
  final Uint8List publicKey;

  /// Throws [CallEncryptionException] if the key pair cannot be made.
  static Future<CallKeyAgreement> create() async {
    try {
      final keyPair = await _x25519.newKeyPair();
      final publicKey = await keyPair.extractPublicKey();
      return CallKeyAgreement._(keyPair, Uint8List.fromList(publicKey.bytes));
    } on Object {
      throw const CallEncryptionException();
    }
  }

  /// The data message that hands this device's public key to the other device.
  List<int> get message => utf8.encode(jsonEncode({'v': _version, 'publicKey': base64Encode(publicKey)}));

  /// The public key in a message from the other device, or null if the message is not
  /// one: anyone in the room can send data, so whatever does not parse is ignored.
  static Uint8List? readMessage(List<int> data) {
    try {
      final decoded = jsonDecode(utf8.decode(data));
      if (decoded case {'v': _version, 'publicKey': final String encoded}) {
        final key = base64Decode(encoded);
        return key.length == _keyLength ? key : null;
      }
    } on FormatException {
      // Falls through to null.
    }

    return null;
  }

  /// The key both devices arrive at with each other's public key. The session id salts
  /// it, so the same two key pairs would still give another call another key. Throws
  /// [CallEncryptionException] for a public key that gives a known secret.
  Future<CallSecret> agree(Uint8List otherPublicKey, String sessionId) async {
    try {
      final shared = await _x25519.sharedSecretKey(
        keyPair: _keyPair,
        remotePublicKey: SimplePublicKey(otherPublicKey, type: KeyPairType.x25519),
      );
      // A public key of small order makes the secret all zeros, which anyone can know.
      if ((await shared.extractBytes()).every((byte) => byte == 0)) {
        throw const CallEncryptionException();
      }

      final key = await _hkdf.deriveKey(secretKey: shared, nonce: utf8.encode(sessionId), info: _info);
      return CallSecret(
        key: Uint8List.fromList(await key.extractBytes()),
        safetyCode: await safetyCodeOf(publicKey, otherPublicKey),
      );
    } on CallEncryptionException {
      rethrow;
    } on Object {
      throw const CallEncryptionException();
    }
  }

  /// Six digits from the two public keys, in an order both devices agree on, so that both
  /// show the same code.
  static Future<String> safetyCodeOf(List<int> first, List<int> second) async {
    final ordered = _compare(first, second) <= 0 ? [...first, ...second] : [...second, ...first];
    final digest = (await Sha256().hash(ordered)).bytes;
    final number = ((digest[0] << 24) | (digest[1] << 16) | (digest[2] << 8) | digest[3]) % 1000000;
    final digits = number.toString().padLeft(6, '0');
    return '${digits.substring(0, 3)} ${digits.substring(3)}';
  }

  static int _compare(List<int> first, List<int> second) {
    for (var i = 0; i < first.length && i < second.length; i++) {
      if (first[i] != second[i]) {
        return first[i] - second[i];
      }
    }

    return first.length - second.length;
  }
}
