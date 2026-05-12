import React from 'react';
import { Badge, Tooltip } from '@fluentui/react-components';
import moment from 'moment';

export type CacheBadgeStatus = 'fresh' | 'stale' | 'never' | 'unknown' | 'inprogress' | 'error';

export interface CacheStatusBadgeProps {
  /** ISO date string of the last refresh */
  lastUpdate?: string | null;
  /** Whether the cached value is fresh according to its TTL (computed server-side). */
  isFresh?: boolean;
  /** Optional override — wins over isFresh / lastUpdate. */
  status?: CacheBadgeStatus;
  /** Short label, e.g. "Directory". Used as the tooltip title and (when not compact) as the prefix. */
  label: string;
  /** Optional supplemental tooltip text (e.g. TTL details). */
  tooltipDetail?: string;
  /**
   * Compact rendering — single-word state (Fresh/Stale/…) with details in the tooltip.
   * Use this inside table cells where horizontal space is tight.
   */
  compact?: boolean;
}

const formatRelative = (iso?: string | null): string => {
  if (!iso) return 'never';
  const m = moment(iso);
  if (!m.isValid()) return 'unknown';
  return m.fromNow();
};

const resolveStatus = (props: CacheStatusBadgeProps): CacheBadgeStatus => {
  if (props.status) return props.status;
  if (!props.lastUpdate) return 'never';
  return props.isFresh ? 'fresh' : 'stale';
};

const colorFor = (s: CacheBadgeStatus): 'success' | 'warning' | 'danger' | 'subtle' | 'informative' => {
  switch (s) {
    case 'fresh': return 'success';
    case 'stale': return 'warning';
    case 'never': return 'subtle';
    case 'inprogress': return 'informative';
    case 'error': return 'danger';
    default: return 'subtle';
  }
};

const shortFor = (s: CacheBadgeStatus): string => {
  switch (s) {
    case 'fresh': return 'Fresh';
    case 'stale': return 'Stale';
    case 'never': return 'Not yet';
    case 'inprogress': return 'Updating';
    case 'error': return 'Error';
    default: return '—';
  }
};

const longFor = (s: CacheBadgeStatus, lastUpdate?: string | null): string => {
  switch (s) {
    case 'fresh': return `updated ${formatRelative(lastUpdate)}`;
    case 'stale': return `stale — ${formatRelative(lastUpdate)}`;
    case 'never': return 'never updated';
    case 'inprogress': return 'updating…';
    case 'error': return 'last update failed';
    default: return formatRelative(lastUpdate);
  }
};

/**
 * Small inline badge that conveys cache freshness at a glance.
 * Use `compact` inside dense table cells; otherwise the badge expands to
 * include the relative-time text.
 */
export const CacheStatusBadge: React.FC<CacheStatusBadgeProps> = (props) => {
  const status = resolveStatus(props);
  const color = colorFor(status);
  const short = shortFor(status);
  const long = longFor(status, props.lastUpdate);

  const tooltipLines = [
    `${props.label}: ${long}`,
    props.lastUpdate ? `Last update: ${moment(props.lastUpdate).format('YYYY-MM-DD HH:mm')}` : null,
    props.tooltipDetail ?? null
  ].filter(Boolean) as string[];

  const text = props.compact ? short : `${props.label}: ${long}`;

  return (
    <Tooltip content={tooltipLines.join(' • ')} relationship="description" withArrow>
      <Badge appearance={props.compact ? 'outline' : 'filled'} color={color} size="small">
        {text}
      </Badge>
    </Tooltip>
  );
};
