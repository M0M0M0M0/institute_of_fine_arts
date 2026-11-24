import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Back button for full-screen pages. It is always visible: it pops when there is a page behind,
/// otherwise (deep link, browser refresh) it goes to `/`, which the router redirects to the
/// signed-in role's home (or the guest home).
class AppBackButton extends StatelessWidget {
  const AppBackButton({super.key});

  @override
  Widget build(BuildContext context) {
    return IconButton(
      tooltip: MaterialLocalizations.of(context).backButtonTooltip,
      icon: const Icon(Icons.arrow_back),
      onPressed: () {
        if (context.canPop()) {
          context.pop();
        } else {
          context.go('/');
        }
      },
    );
  }
}
