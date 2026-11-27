import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../app/tokens.dart';
import 'scroll_fade.dart';

/// Section switch as quiet small-caps tabs with a brass rule under the current one and, optionally, a number
/// next to each name. [expand] spreads the tabs over the full width (phones); otherwise they sit on the left and
/// the hairline under them still runs the full width.
class SegmentTabs<T> extends StatelessWidget {
  const SegmentTabs({
    super.key,
    required this.values,
    required this.selected,
    required this.onSelected,
    required this.labelOf,
    this.countOf,
    this.expand = false,
  });

  final List<T> values;
  final T selected;
  final ValueChanged<T> onSelected;
  final String Function(T value) labelOf;

  /// Number shown next to a tab name; null for no number.
  final int? Function(T value)? countOf;
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;

    Widget tab(T value) {
      final active = value == selected;
      final count = countOf?.call(value);
      final label = Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            labelOf(value).toUpperCase(),
            style: text.labelSmall!.copyWith(
              fontSize: 12.5,
              letterSpacing: 1.6,
              color: active ? AppColors.ink : AppColors.muted,
            ),
          ),
          if (count != null) ...[
            const SizedBox(width: 6),
            Text(
              '$count',
              style: text.labelSmall!.copyWith(
                fontSize: 12.5,
                letterSpacing: 0,
                fontWeight: FontWeight.w500,
                color: active ? AppColors.brassText : AppColors.muted,
              ),
            ),
          ],
        ],
      );
      return Semantics(
        button: true,
        selected: active,
        child: InkWell(
          onTap: () {
            if (!active) HapticFeedback.selectionClick();
            onSelected(value);
          },
          child: Container(
            height: AppSpacing.touch,
            // Tight side padding when the tabs share the row, and the label shrinks if the window is very narrow
            padding: EdgeInsets.symmetric(
              horizontal: expand ? AppSpacing.xs : AppSpacing.md,
            ),
            alignment: Alignment.center,
            decoration: BoxDecoration(
              border: Border(
                bottom: BorderSide(
                  color: active ? AppColors.brass : Colors.transparent,
                  width: 2,
                ),
              ),
            ),
            child: FittedBox(fit: BoxFit.scaleDown, child: label),
          ),
        ),
      );
    }

    final tabs = [for (final v in values) tab(v)];
    return DecoratedBox(
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: AppColors.line)),
      ),
      child: expand
          ? Row(children: [for (final t in tabs) Expanded(child: t)])
          : Row(children: [...tabs, const Spacer()]),
    );
  }
}

/// A row of filter words that scrolls sideways: small caps, the current one in ink with a brass rule under it.
/// For lists with many filters (statuses), where [SegmentTabs] would not fit. Pass the current one as [selected].
/// When it first appears the row scrolls to the current word, so a filter chosen elsewhere (for example from a card
/// on the home page) is never hidden off screen.
class FilterTabs<T> extends StatefulWidget {
  const FilterTabs({
    super.key,
    required this.values,
    required this.selected,
    required this.onSelected,
    required this.labelOf,
  });

  final List<T> values;
  final T selected;
  final ValueChanged<T> onSelected;
  final String Function(T value) labelOf;

  @override
  State<FilterTabs<T>> createState() => _FilterTabsState<T>();
}

class _FilterTabsState<T> extends State<FilterTabs<T>> {
  // Marks the current word, to bring it into view
  final _currentKey = GlobalKey();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final context = _currentKey.currentContext;
      if (context != null && context.mounted) {
        Scrollable.ensureVisible(
          context,
          alignment: 0.5,
          duration: Duration.zero,
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final selected = widget.selected;
    return DecoratedBox(
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: AppColors.line)),
      ),
      child: SizedBox(
        height: AppSpacing.touch,
        // A plain scroll view with a Row (not a lazy list): there are only a few words and all of them must exist
        child: ScrollEdgeFade(
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (final v in widget.values)
                  KeyedSubtree(
                    key: v == selected ? _currentKey : null,
                    child: Semantics(
                      button: true,
                      selected: v == selected,
                      child: InkWell(
                        onTap: () {
                          if (v != selected) HapticFeedback.selectionClick();
                          widget.onSelected(v);
                        },
                        child: Container(
                          height: AppSpacing.touch,
                          alignment: Alignment.center,
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.md,
                          ),
                          decoration: BoxDecoration(
                            border: Border(
                              bottom: BorderSide(
                                color: v == selected
                                    ? AppColors.brass
                                    : Colors.transparent,
                                width: 2,
                              ),
                            ),
                          ),
                          child: Text(
                            widget.labelOf(v).toUpperCase(),
                            style: text.labelSmall!.copyWith(
                              fontSize: 12.5,
                              letterSpacing: 1.6,
                              color: v == selected
                                  ? AppColors.ink
                                  : AppColors.muted,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
