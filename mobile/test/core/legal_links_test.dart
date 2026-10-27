import 'package:exparter/core/legal/legal_documents.dart';
import 'package:exparter/core/legal/legal_links.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../helpers.dart';

class _RecordingOpener implements LegalDocumentOpener {
  _RecordingOpener({required this.opens});

  final bool opens;
  final List<LegalDocument> opened = [];

  @override
  Future<bool> open(LegalDocument document) async {
    opened.add(document);
    return opens;
  }
}

void main() {
  final l10n = AppLocalizationsTr();

  Future<void> pump(WidgetTester tester, _RecordingOpener opener) => tester.pumpWidget(
        ProviderScope(
          overrides: [legalDocumentOpenerProvider.overrideWithValue(opener)],
          child: localized(const Scaffold(body: LegalLinks(documents: LegalDocument.values))),
        ),
      );

  test('documents are pages of the public site', () {
    expect(LegalDocument.terms.uri.toString(), 'https://exparter.app/kullanim-kosullari.html');
    expect(LegalDocument.privacy.uri.toString(), 'https://exparter.app/gizlilik.html');
    expect(LegalDocument.dataNotice.uri.toString(), 'https://exparter.app/kvkk.html');
  });

  testWidgets('opens the document that was tapped', (tester) async {
    final opener = _RecordingOpener(opens: true);
    await pump(tester, opener);

    await tester.tap(find.text(l10n.legalTerms));
    await tester.tap(find.text(l10n.legalDataNotice));
    await tester.pumpAndSettle();

    expect(opener.opened, [LegalDocument.terms, LegalDocument.dataNotice]);
    expect(find.text(l10n.legalOpenFailed), findsNothing);
  });

  testWidgets('says so when the page cannot be opened', (tester) async {
    await pump(tester, _RecordingOpener(opens: false));

    await tester.tap(find.text(l10n.legalPrivacy));
    await tester.pumpAndSettle();

    expect(find.text(l10n.legalOpenFailed), findsOneWidget);
  });
}
