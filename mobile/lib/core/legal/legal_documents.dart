import 'package:riverpod_annotation/riverpod_annotation.dart';
import 'package:url_launcher/url_launcher.dart';

import '../config/app_config.dart';

part 'legal_documents.g.dart';

/// The texts on the public site (deploy/site) that the app links to. They live on the
/// site, not in the app, so that the store listing, the app and the web show one text.
enum LegalDocument {
  terms('kullanim-kosullari.html'),
  privacy('gizlilik.html'),
  dataNotice('kvkk.html');

  const LegalDocument(this.page);

  final String page;

  Uri get uri => Uri.parse('${AppConfig.siteBaseUrl}/$page');
}

/// Opens a document in the phone's browser, inside the app.
abstract interface class LegalDocumentOpener {
  /// False if no browser could open it.
  Future<bool> open(LegalDocument document);
}

@Riverpod(keepAlive: true)
LegalDocumentOpener legalDocumentOpener(Ref ref) => const _UrlLauncherOpener();

class _UrlLauncherOpener implements LegalDocumentOpener {
  const _UrlLauncherOpener();

  @override
  Future<bool> open(LegalDocument document) async {
    try {
      return await launchUrl(document.uri, mode: LaunchMode.inAppBrowserView);
    } on Exception {
      return false;
    }
  }
}
