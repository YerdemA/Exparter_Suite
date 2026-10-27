import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/app_localizations.dart';
import 'legal_documents.dart';

/// Buttons that open the given legal documents; tells the user if one would not open.
class LegalLinks extends ConsumerWidget {
  const LegalLinks({required this.documents, this.alignment = WrapAlignment.start, super.key});

  final List<LegalDocument> documents;
  final WrapAlignment alignment;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return Wrap(
      alignment: alignment,
      children: [
        for (final document in documents)
          TextButton(
            onPressed: () async {
              final messenger = ScaffoldMessenger.of(context);
              if (!await ref.read(legalDocumentOpenerProvider).open(document)) {
                messenger.showSnackBar(SnackBar(content: Text(l10n.legalOpenFailed)));
              }
            },
            child: Text(_titleOf(l10n, document)),
          ),
      ],
    );
  }

  static String _titleOf(AppLocalizations l10n, LegalDocument document) => switch (document) {
        LegalDocument.terms => l10n.legalTerms,
        LegalDocument.privacy => l10n.legalPrivacy,
        LegalDocument.dataNotice => l10n.legalDataNotice,
      };
}
