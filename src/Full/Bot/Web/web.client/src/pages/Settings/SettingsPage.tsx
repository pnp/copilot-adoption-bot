import React, { useState, useEffect } from 'react';
import {
    Button,
    Card,
    CardHeader,
    Spinner,
    Text,
    Textarea,
    makeStyles,
    tokens,
    Badge,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
} from '@fluentui/react-components';
import { 
    Settings20Regular, 
    ArrowReset20Regular, 
    Sparkle20Regular,
    ArrowSync20Regular,
    Delete20Regular,
} from '@fluentui/react-icons';
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import { getSettings, updateSettings, resetSettingsToDefaults, getCopilotConnectedStatus, clearUserCache, syncUserCache, getCachedUsers, updateCopilotStats, clearCopilotStats, getUserCacheStatus } from '../../api/ApiCalls';
import { AppSettingsDto, CopilotConnectedStatusDto, CacheStatusDto } from '../../apimodels/Models';
import { CacheStatusBadge } from '../../components/common/controls/CacheStatusBadge';

const useStyles = makeStyles({
    container: {
        padding: tokens.spacingVerticalXXL,
        maxWidth: '900px',
    },
    header: {
        marginBottom: tokens.spacingVerticalL,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    card: {
        marginBottom: tokens.spacingVerticalL,
    },
    formField: {
        marginBottom: tokens.spacingVerticalM,
    },
    textarea: {
        width: '100%',
        minHeight: '300px',
        maxHeight: 'none !important',
        fontFamily: 'monospace',

        '& textarea': {
            maxHeight: 'none !important',
            minHeight: '300px',
        },
    },
    buttonContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        marginTop: tokens.spacingVerticalL,
    },
    error: {
        color: tokens.colorPaletteRedForeground1,
        marginBottom: tokens.spacingVerticalM,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        marginBottom: tokens.spacingVerticalM,
    },
    infoCard: {
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginBottom: tokens.spacingVerticalM,
    },
    copilotBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        marginBottom: tokens.spacingVerticalM,
    },
});

interface SettingsPageProps {
    loader?: BaseAxiosApiLoader;
}

export const SettingsPage: React.FC<SettingsPageProps> = ({ loader }) => {
    const styles = useStyles();

    const [settings, setSettings] = useState<AppSettingsDto | null>(null);
    const [copilotStatus, setCopilotStatus] = useState<CopilotConnectedStatusDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [cacheOperationInProgress, setCacheOperationInProgress] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);
    const [cachedUserCount, setCachedUserCount] = useState<number | null>(null);
    const [copilotStatsStatus, setCopilotStatsStatus] = useState<string | null>(null);
    const [copilotStatsLastUpdate, setCopilotStatsLastUpdate] = useState<string | null>(null);
    const [cacheStatus, setCacheStatus] = useState<CacheStatusDto | null>(null);

    // Form state
    const [followUpChatPrompt, setFollowUpChatPrompt] = useState<string>('');
    const [hasChanges, setHasChanges] = useState(false);

    useEffect(() => {
        loadData();
    }, [loader]);

    // Auto-refresh the cache status panel every 30s so the badges stay live.
    useEffect(() => {
        if (!loader) return;
        const id = window.setInterval(() => {
            loadCacheInfo();
        }, 30_000);
        return () => window.clearInterval(id);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [loader]);

    const loadData = async () => {
        if (!loader) return;

        try {
            setLoading(true);
            setError(null);

            const [settingsData, copilotData] = await Promise.all([
                getSettings(loader),
                getCopilotConnectedStatus(loader),
            ]);

            setSettings(settingsData);
            setCopilotStatus(copilotData);
            
            // Initialize form with current value or default prompt if no custom value
            setFollowUpChatPrompt(settingsData.followUpChatSystemPrompt || settingsData.defaultFollowUpChatSystemPrompt);
            setHasChanges(false);

            // Load cached user count
            await loadCacheInfo();
        } catch (err: any) {
            setError(err.message || 'Failed to load settings');
            console.error('Error loading settings:', err);
        } finally {
            setLoading(false);
        }
    };

    const loadCacheInfo = async () => {
        if (!loader) return;

        try {
            // Prefer the dedicated Status endpoint — it's cheap and returns rich metadata.
            const status = await getUserCacheStatus(loader);
            setCacheStatus(status);
            setCachedUserCount(status.cachedUserCount);
            if (status.lastCopilotStatsUpdate) {
                setCopilotStatsLastUpdate(new Date(status.lastCopilotStatsUpdate).toLocaleString());
            }
        } catch (err: any) {
            // Older server without /Status — fall back to the user list.
            console.warn('Cache status endpoint failed; falling back to user count', err);
            try {
                const users = await getCachedUsers(loader);
                setCachedUserCount(users.length);
            } catch (innerErr) {
                console.error('Error loading cache info:', innerErr);
                setCachedUserCount(null);
            }
        }
    };

    const handlePromptChange = (value: string) => {
        setFollowUpChatPrompt(value);
        // Check if there are changes from the saved value (or default if none saved)
        const savedValue = settings?.followUpChatSystemPrompt || settings?.defaultFollowUpChatSystemPrompt || '';
        setHasChanges(value !== savedValue);
    };

    const handleSave = async () => {
        if (!loader || !settings) return;

        try {
            setSaving(true);
            setError(null);
            setSuccess(null);

            // If the prompt matches the default, save as null (to use default)
            const promptToSave = followUpChatPrompt.trim() === settings.defaultFollowUpChatSystemPrompt 
                ? null 
                : (followUpChatPrompt.trim() || null);

            const updatedSettings = await updateSettings(loader, {
                followUpChatSystemPrompt: promptToSave,
            });

            setSettings(updatedSettings);
            setFollowUpChatPrompt(updatedSettings.followUpChatSystemPrompt || updatedSettings.defaultFollowUpChatSystemPrompt);
            setHasChanges(false);
            setSuccess('Settings saved successfully!');
        } catch (err: any) {
            setError(err.message || 'Failed to save settings');
            console.error('Error saving settings:', err);
        } finally {
            setSaving(false);
        }
    };

    const handleResetToDefaults = async () => {
        if (!loader || !settings) return;

        if (!window.confirm('Are you sure you want to reset the system prompt to the default? This will discard any custom prompt.')) {
            return;
        }

        try {
            setSaving(true);
            setError(null);
            setSuccess(null);

            const updatedSettings = await resetSettingsToDefaults(loader);

            setSettings(updatedSettings);
            setFollowUpChatPrompt(updatedSettings.defaultFollowUpChatSystemPrompt);
            setHasChanges(false);
            setSuccess('Settings reset to defaults successfully!');
        } catch (err: any) {
            setError(err.message || 'Failed to reset settings');
            console.error('Error resetting settings:', err);
        } finally {
            setSaving(false);
        }
    };

    const handleClearCache = async () => {
        if (!loader) return;

        if (!window.confirm('Are you sure you want to clear the user cache? This will force a full resync on next access.')) {
            return;
        }

        try {
            setCacheOperationInProgress(true);
            setError(null);
            setSuccess(null);

            const result = await clearUserCache(loader);
            setSuccess(result.message);
            setCachedUserCount(0);
        } catch (err: any) {
            setError(err.message || 'Failed to clear cache');
            console.error('Error clearing cache:', err);
        } finally {
            setCacheOperationInProgress(false);
        }
    };

    const handleSyncCache = async () => {
        if (!loader) return;

        try {
            setCacheOperationInProgress(true);
            setError(null);
            setSuccess(null);

            const result = await syncUserCache(loader);
            setSuccess(result.message);
            
            // Reload cache info after sync
            await loadCacheInfo();
        } catch (err: any) {
            setError(err.message || 'Failed to sync cache');
            console.error('Error syncing cache:', err);
        } finally {
            setCacheOperationInProgress(false);
        }
    };

    const handleUpdateCopilotStats = async () => {
        if (!loader) return;

        try {
            setCacheOperationInProgress(true);
            setError(null);
            setSuccess(null);
            setCopilotStatsStatus('Updating...');

            const result = await updateCopilotStats(loader);
            
            if (result.success) {
                setSuccess(result.message);
                setCopilotStatsStatus('Success');
                if (result.lastUpdate) {
                    setCopilotStatsLastUpdate(new Date(result.lastUpdate).toLocaleString());
                }
            } else {
                // Handle known error scenarios
                if (result.error?.includes('Forbidden') || result.error?.includes('403')) {
                    setError('Permission denied: Reports.Read.All permission may not be granted. Check Azure AD app permissions.');
                    setCopilotStatsStatus('Permission Denied');
                } else if (result.error?.includes('404')) {
                    setError('Copilot stats endpoint not found. Your tenant may not have access to this feature.');
                    setCopilotStatsStatus('Not Available');
                } else {
                    setError(result.error || 'Failed to update Copilot stats');
                    setCopilotStatsStatus('Failed');
                }
            }
        } catch (err: any) {
            const errorMessage = err.message || 'Failed to update Copilot stats';
            setError(errorMessage);
            setCopilotStatsStatus('Error');
            console.error('Error updating Copilot stats:', err);
        } finally {
            setCacheOperationInProgress(false);
        }
    };

    const handleClearCopilotStats = async () => {
        if (!loader) return;

        if (!window.confirm('Are you sure you want to clear Copilot stats metadata? This will force a fresh data pull on the next update.')) {
            return;
        }

        try {
            setCacheOperationInProgress(true);
            setError(null);
            setSuccess(null);

            const result = await clearCopilotStats(loader);
            setSuccess(result.message);
        } catch (err: any) {
            setError(err.message || 'Failed to clear Copilot stats');
            console.error('Error clearing Copilot stats:', err);
        } finally {
            setCacheOperationInProgress(false);
        }
    };

    if (loading) {
        return (
            <div className={styles.container}>
                <Spinner label="Loading settings..." />
            </div>
        );
    }

    const isUsingDefault = !settings?.followUpChatSystemPrompt;

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Settings20Regular />
                <h1>Settings</h1>
            </div>

            {!copilotStatus?.isEnabled && (
                <MessageBar intent="warning" style={{ marginBottom: tokens.spacingVerticalM }}>
                    <MessageBarBody>
                        <MessageBarTitle>AI Features Not Configured</MessageBarTitle>
                        These settings apply to AI-powered features which require AI Foundry configuration.
                        Configure AI Foundry in your application settings to enable these features.
                    </MessageBarBody>
                </MessageBar>
            )}

            {copilotStatus?.isEnabled && (
                <div className={styles.copilotBadge}>
                    <Sparkle20Regular />
                    <Badge appearance="filled" color="brand">Copilot Connected</Badge>
                    <Text size={200}>AI features are enabled</Text>
                </div>
            )}

            {error && <div className={styles.error}>{error}</div>}
            {success && <div className={styles.success}>{success}</div>}

            {/* User Cache Management Card */}
            <Card className={styles.card}>
                <CardHeader 
                    header={<Text weight="semibold">User Cache Management</Text>}
                    description={
                        <Text size={200}>
                            Manage the Microsoft Graph user cache. The cache stores user information to improve performance.
                        </Text>
                    }
                />

                {cachedUserCount !== null && (
                    <div style={{ marginBottom: tokens.spacingVerticalM }}>
                        <Text size={300}>
                            <strong>Cached Users:</strong> {cachedUserCount.toLocaleString()}
                        </Text>
                    </div>
                )}

                {cacheStatus && (
                    <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, flexWrap: 'wrap', marginBottom: tokens.spacingVerticalM }}>
                        <CacheStatusBadge
                            label="Directory"
                            lastUpdate={cacheStatus.lastDeltaSyncDate ?? null}
                            isFresh={cacheStatus.isUserCacheFresh}
                            status={cacheStatus.isSyncInProgress ? 'inprogress' : (!cacheStatus.lastDeltaSyncDate ? 'never' : (cacheStatus.isUserCacheFresh ? 'fresh' : 'stale'))}
                            tooltipDetail={`Refresh interval: ${Math.round(cacheStatus.userCacheTtlSeconds / 60)} min. A background warm-up keeps this fresh.`}
                        />
                        <CacheStatusBadge
                            label="Copilot stats"
                            lastUpdate={cacheStatus.lastCopilotStatsUpdate ?? null}
                            isFresh={cacheStatus.isCopilotStatsFresh}
                            status={!cacheStatus.lastCopilotStatsUpdate ? 'never' : (cacheStatus.isCopilotStatsFresh ? 'fresh' : 'stale')}
                            tooltipDetail={`Refresh interval: ${Math.round(cacheStatus.copilotStatsTtlSeconds / 3600)} hours. Graph reports lag by up to ~48h.`}
                        />
                        {cacheStatus.lastFullSyncDate && (
                            <CacheStatusBadge
                                label="Full sync"
                                lastUpdate={cacheStatus.lastFullSyncDate}
                                status="fresh"
                                tooltipDetail={`Full re-sync interval: ${Math.round(cacheStatus.fullSyncIntervalSeconds / 86400)} days.`}
                            />
                        )}
                        {cacheStatus.lastSyncError && (
                            <CacheStatusBadge
                                label="Last sync"
                                status="error"
                                tooltipDetail={cacheStatus.lastSyncError}
                            />
                        )}
                    </div>
                )}

                {copilotStatsStatus && (
                    <div style={{ marginBottom: tokens.spacingVerticalM }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                            <Text size={300}>
                                <strong>Copilot Stats Status:</strong>
                            </Text>
                            <Badge 
                                appearance="filled" 
                                color={
                                    copilotStatsStatus === 'Success' ? 'success' :
                                    copilotStatsStatus === 'Permission Denied' ? 'danger' :
                                    copilotStatsStatus === 'Failed' || copilotStatsStatus === 'Error' ? 'danger' :
                                    copilotStatsStatus === 'Not Available' ? 'warning' :
                                    'informative'
                                }
                            >
                                {copilotStatsStatus}
                            </Badge>
                        </div>
                        {copilotStatsLastUpdate && (
                            <Text size={200} style={{ marginTop: tokens.spacingVerticalXS, display: 'block' }}>
                                Last updated: {copilotStatsLastUpdate}
                            </Text>
                        )}
                    </div>
                )}

                {copilotStatsStatus === 'Permission Denied' && (
                    <MessageBar intent="error" style={{ marginBottom: tokens.spacingVerticalM }}>
                        <MessageBarBody>
                            <MessageBarTitle>Permission Required</MessageBarTitle>
                            The Reports.Read.All permission is required to fetch Copilot usage statistics.
                            Please grant this permission in your Azure AD app registration and try again.
                        </MessageBarBody>
                    </MessageBar>
                )}

                {copilotStatsStatus === 'Not Available' && (
                    <MessageBar intent="warning" style={{ marginBottom: tokens.spacingVerticalM }}>
                        <MessageBarBody>
                            <MessageBarTitle>Feature Not Available</MessageBarTitle>
                            Copilot usage statistics are not available for your tenant.
                            This feature requires Microsoft 365 Copilot licenses and may not be available in all regions.
                        </MessageBarBody>
                    </MessageBar>
                )}

                <div className={styles.buttonContainer}>
                    <Button
                        appearance="secondary"
                        icon={<ArrowSync20Regular />}
                        onClick={handleSyncCache}
                        disabled={cacheOperationInProgress}
                    >
                        {cacheOperationInProgress ? 'Syncing...' : 'Sync Cache'}
                    </Button>
                    <Button
                        appearance="secondary"
                        icon={<Sparkle20Regular />}
                        onClick={handleUpdateCopilotStats}
                        disabled={cacheOperationInProgress}
                    >
                        {cacheOperationInProgress ? 'Updating...' : 'Update Copilot Stats'}
                    </Button>
                    <Button
                        appearance="secondary"
                        icon={<ArrowReset20Regular />}
                        onClick={handleClearCopilotStats}
                        disabled={cacheOperationInProgress}
                    >
                        {cacheOperationInProgress ? 'Clearing...' : 'Clear Copilot Stats'}
                    </Button>
                    <Button
                        appearance="secondary"
                        icon={<Delete20Regular />}
                        onClick={handleClearCache}
                        disabled={cacheOperationInProgress}
                    >
                        {cacheOperationInProgress ? 'Clearing...' : 'Clear Cache'}
                    </Button>
                </div>
            </Card>

            {/* Follow-up Chat System Prompt Card */}
            <Card className={styles.card}>
                <CardHeader 
                    header={<Text weight="semibold">Follow-up Chat System Prompt</Text>}
                    description={
                        <Text size={200}>
                            Configure the system prompt used when users reply to nudge messages.
                            This prompt instructs the AI on how to respond to user questions and feedback.
                        </Text>
                    }
                />

                <div className={styles.formField}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalS }}>
                        <Badge 
                            appearance="filled" 
                            color={isUsingDefault ? 'informative' : 'brand'}
                        >
                            {isUsingDefault ? 'Default Prompt' : 'Custom Prompt'}
                        </Badge>
                    </div>
                    <Textarea
                        id="followUpChatPrompt"
                        value={followUpChatPrompt}
                        onChange={(e) => handlePromptChange(e.target.value)}
                        resize="vertical"
                        className={styles.textarea}
                    />
                </div>

                <div className={styles.buttonContainer}>
                    <Button 
                        appearance="primary" 
                        onClick={handleSave} 
                        disabled={!hasChanges || saving}
                    >
                        {saving ? 'Saving...' : 'Save Changes'}
                    </Button>
                    <Button 
                        appearance="secondary" 
                        onClick={handleResetToDefaults} 
                        disabled={saving}
                    >
                        {saving ? 'Resetting...' : 'Reset to Defaults'}
                    </Button>
                </div>
            </Card>
        </div>
    );
};
