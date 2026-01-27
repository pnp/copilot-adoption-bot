import React from 'react';
import 'chartjs-adapter-date-fns'
import { MessageStatusStatsDto, UserCoverageStatsDto, CopilotConnectedStatusDto } from '../../apimodels/Models';
import { Badge, Button, Caption1, Card, CardHeader, Spinner, Text } from '@fluentui/react-components';
import { ChartContainer } from '../../components/app/ChartContainer';
import { MoreHorizontal20Regular, Sparkle20Regular, PlugDisconnected20Regular } from "@fluentui/react-icons";
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import { useStyles } from '../../utils/styles';
import { getMessageStatusStats, getUserCoverageStats, getCopilotConnectedStatus } from '../../api/ApiCalls';
import { MessageStatusChart } from './MessageStatusChart';
import { UserCoverageChart } from './UserCoverageChart';


export const Dashboard: React.FC<{ loader?: BaseAxiosApiLoader }> = (props) => {

    const [messageStats, setMessageStats] = React.useState<MessageStatusStatsDto | null>(null);
    const [userStats, setUserStats] = React.useState<UserCoverageStatsDto | null>(null);
    const [copilotStatus, setCopilotStatus] = React.useState<CopilotConnectedStatusDto | null>(null);
    const [error, setError] = React.useState<string | null>(null);
    const [loadingStats, setLoadingStats] = React.useState(true);
    const styles = useStyles();

    React.useEffect(() => {
        if (props.loader) {
            // Load statistics
            loadStatistics();
        }
    }, [props.loader]);

    const loadStatistics = async () => {
        if (!props.loader) return;

        try {
            setLoadingStats(true);
            const [msgStats, usrStats, copilotConnected] = await Promise.all([
                getMessageStatusStats(props.loader),
                getUserCoverageStats(props.loader),
                getCopilotConnectedStatus(props.loader)
            ]);
            setMessageStats(msgStats);
            setUserStats(usrStats);
            setCopilotStatus(copilotConnected);
        } catch (e: any) {
            console.error("Error loading statistics: ", e);
            setError(e.toString());
        } finally {
            setLoadingStats(false);
        }
    };

    return (
        <div>
            <section className="page--header">
                <div className="page-title">
                    <h1>Office Adoption Bot Dashboard</h1>

                    <p>Welcome to the Office Adoption Bot control panel. View message statistics and user coverage below.</p>

                    {/* Copilot Connected Status */}
                    {copilotStatus && (
                        <Card style={{ marginBottom: '16px', maxWidth: '400px' }}>
                            <CardHeader
                                image={copilotStatus.isEnabled ? <Sparkle20Regular /> : <PlugDisconnected20Regular />}
                                header={
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                        <Text weight="semibold">AI Agents</Text>
                                        <Badge 
                                            appearance="filled" 
                                            color={copilotStatus.isEnabled ? "success" : "subtle"}
                                        >
                                            {copilotStatus.isEnabled ? "Connected" : "Not Configured"}
                                        </Badge>
                                    </div>
                                }
                                description={
                                    <Caption1>
                                        {copilotStatus.isEnabled 
                                            ? "Smart groups and AI follow-up chats are available" 
                                            : "Configure AI Foundry to enable smart groups"}
                                    </Caption1>
                                }
                            />
                        </Card>
                    )}

                    {error ? <p className='error'>{error}</p>
                        :
                        <div>
                            <ChartContainer>
                                        <div className='nav'>
                                            <ul>
                                                <li>
                                                    <Card className={styles.card}>
                                                        <CardHeader
                                                            header={<Text weight="semibold">Message Status</Text>}
                                                            description={
                                                                <Caption1 className={styles.caption}>Sent, Failed, and Pending Messages</Caption1>
                                                            }
                                                            action={
                                                                <Button
                                                                    appearance="transparent"
                                                                    icon={<MoreHorizontal20Regular />}
                                                                    aria-label="More options"
                                                                />
                                                            }
                                                        />

                                                        {loadingStats ? (
                                                            <Spinner label="Loading statistics..." />
                                                        ) : messageStats ? (
                                                            <>
                                                                <p className={styles.text}>
                                                                    <strong>Total Messages:</strong> {messageStats.totalCount}<br />
                                                                    <strong>Sent:</strong> {messageStats.sentCount} | 
                                                                    <strong> Failed:</strong> {messageStats.failedCount} | 
                                                                    <strong> Pending:</strong> {messageStats.pendingCount}
                                                                </p>
                                                                <MessageStatusChart stats={messageStats} />
                                                            </>
                                                        ) : (
                                                            <p className={styles.text}>No data available</p>
                                                        )}
                                                    </Card>
                                                </li>
                                                <li>
                                                    <Card className={styles.card}>
                                                        <CardHeader
                                                            header={<Text weight="semibold">User Coverage</Text>}
                                                            description={
                                                                <Caption1 className={styles.caption}>Users Messaged vs Total Users in Tenant</Caption1>
                                                            }
                                                            action={
                                                                <Button
                                                                    appearance="transparent"
                                                                    icon={<MoreHorizontal20Regular />}
                                                                    aria-label="More options"
                                                                />
                                                            }
                                                        />

                                                        {loadingStats ? (
                                                            <Spinner label="Loading statistics..." />
                                                        ) : userStats ? (
                                                            <>
                                                                <p className={styles.text}>
                                                                    <strong>Total Users in Tenant:</strong> {userStats.totalUsersInTenant}<br />
                                                                    <strong>Users Messaged:</strong> {userStats.usersMessaged}<br />
                                                                    <strong>Coverage:</strong> {userStats.coveragePercentage.toFixed(2)}%
                                                                </p>
                                                                <UserCoverageChart stats={userStats} />
                                                            </>
                                                        ) : (
                                                            <p className={styles.text}>No data available</p>
                                                        )}
                                                    </Card>
                                                </li>
                                            </ul>
                                        </div>
                                    </ChartContainer>
                                </div>
                    }

                </div >
            </section >

        </div >
    );
};
