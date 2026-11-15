import '../core/config.dart';

/// Builds absolute URLs for the public image endpoints of the web app (no token needed).
///
/// Only use these for images that are public: competition covers, and artworks of
/// published competitions (award winners / BEST). Private images go through `AuthImage`.
abstract final class ApiUrls {
  static String _join(String path) {
    final base = apiBaseUrl.endsWith('/') ? apiBaseUrl.substring(0, apiBaseUrl.length - 1) : apiBaseUrl;
    return '$base$path';
  }

  /// Competition cover image: `/files/competitions/{id}/cover`.
  static String competitionCover(int competitionId) => _join('/files/competitions/$competitionId/cover');

  /// 400 px thumbnail of a public artwork: `/files/submissions/{id}/thumb`.
  static String submissionThumb(int submissionId) => _join('/files/submissions/$submissionId/thumb');

  /// Full-size public artwork: `/files/submissions/{id}`.
  static String submissionFull(int submissionId) => _join('/files/submissions/$submissionId');

  /// Thumbnail or full-size depending on [thumb].
  static String submission(int submissionId, {bool thumb = true}) =>
      thumb ? submissionThumb(submissionId) : submissionFull(submissionId);
}
