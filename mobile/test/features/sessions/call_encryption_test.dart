import 'dart:convert';
import 'dart:typed_data';

import 'package:exparter/features/sessions/domain/call_encryption.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('both devices arrive at the same key and safety code', () async {
    final ali = await CallKeyAgreement.create();
    final ayse = await CallKeyAgreement.create();

    final aliSide = await ali.agree(CallKeyAgreement.readMessage(ayse.message)!, 'session-1');
    final ayseSide = await ayse.agree(CallKeyAgreement.readMessage(ali.message)!, 'session-1');

    expect(aliSide.key, hasLength(32));
    expect(aliSide.key, ayseSide.key);
    expect(aliSide.safetyCode, ayseSide.safetyCode);
    expect(aliSide.safetyCode, matches(RegExp(r'^\d{3} \d{3}$')));
  });

  test('another call or another key pair gives another key and code', () async {
    final ali = await CallKeyAgreement.create();
    final ayse = await CallKeyAgreement.create();
    final stranger = await CallKeyAgreement.create();

    final first = await ali.agree(ayse.publicKey, 'session-1');
    final otherSession = await ali.agree(ayse.publicKey, 'session-2');
    final otherPeer = await ali.agree(stranger.publicKey, 'session-1');

    expect(otherSession.key, isNot(first.key));
    expect(otherSession.safetyCode, first.safetyCode);
    expect(otherPeer.key, isNot(first.key));
    expect(otherPeer.safetyCode, isNot(first.safetyCode));
  });

  test('anything but a well-formed key message is ignored', () {
    List<int> encode(Object value) => utf8.encode(jsonEncode(value));

    expect(CallKeyAgreement.readMessage(utf8.encode('hello')), isNull);
    expect(CallKeyAgreement.readMessage([0xff, 0xfe]), isNull);
    expect(CallKeyAgreement.readMessage(encode({'v': 2, 'publicKey': base64Encode(List.filled(32, 1))})), isNull);
    expect(CallKeyAgreement.readMessage(encode({'v': 1, 'publicKey': base64Encode(List.filled(16, 1))})), isNull);
    expect(CallKeyAgreement.readMessage(encode({'v': 1, 'publicKey': 'not base64!'})), isNull);
    expect(CallKeyAgreement.readMessage(encode({'v': 1, 'publicKey': base64Encode(List.filled(32, 1))})), hasLength(32));
  });

  test('a public key that makes the secret known to anyone is refused', () async {
    final ali = await CallKeyAgreement.create();

    await expectLater(ali.agree(Uint8List(32), 'session-1'), throwsA(isA<CallEncryptionException>()));
  });
}
