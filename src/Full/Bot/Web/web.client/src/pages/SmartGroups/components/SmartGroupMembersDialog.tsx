import React, { useState, useEffect } from 'react';
import {
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    Button,
    Spinner,
    Text,
    makeStyles,
    tokens,
    Badge,
    Table,
    TableBody,
    TableCell,
    TableRow,
    TableHeader,
    TableHeaderCell,
} from '@fluentui/react-components';
import {
    ArrowSyncRegular,
    DismissRegular,
    CheckmarkCircleRegular,
    CloudArrowUpRegular,
} from '@fluentui/react-icons';
import { BaseAxiosApiLoader } from '../../../api/AxiosApiLoader';
import { resolveSmartGroupMembers } from '../../../api/ApiCalls';
import { SmartGroupResolutionResult } from '../../../apimodels/Models';

const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '95vw',
        width: '1200px',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
    },
    memberTable: {
        marginTop: tokens.spacingVerticalM,
        overflowX: 'auto',
    },
    emptyState: {
        textAlign: 'center',
        padding: tokens.spacingVerticalXXL,
        color: tokens.colorNeutralForeground3,
    },
    cacheInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        marginBottom: tokens.spacingVerticalM,
    },
    errorMessage: {
        color: tokens.colorPaletteRedForeground1,
        marginTop: tokens.spacingVerticalM,
    },
    confidenceScore: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    truncatedText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '150px',
        display: 'block',
    },
    wideColumn: {
        minWidth: '120px',
    },
    narrowColumn: {
        minWidth: '100px',
    },
});

interface SmartGroupMembersDialogProps {
    open: boolean;
    onClose: () => void;
    groupId: string;
    groupName: string;
    loader: BaseAxiosApiLoader;
}

export const SmartGroupMembersDialog: React.FC<SmartGroupMembersDialogProps> = ({
    open,
    onClose,
    groupId,
    groupName,
    loader,
}) => {
    const styles = useStyles();
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [result, setResult] = useState<SmartGroupResolutionResult | null>(null);

    useEffect(() => {
        if (open) {
            loadMembers(false);
        }
    }, [open, groupId]);

    const loadMembers = async (forceRefresh: boolean) => {
        try {
            if (forceRefresh) {
                setRefreshing(true);
            } else {
                setLoading(true);
            }
            setError(null);
            
            const data = await resolveSmartGroupMembers(loader, groupId, forceRefresh);
            setResult(data);
        } catch (err: any) {
            setError(err.message || 'Failed to load members');
            console.error('Error loading members:', err);
        } finally {
            setLoading(false);
            setRefreshing(false);
        }
    };

    const handleForceRefresh = () => {
        loadMembers(true);
    };

    const formatDate = (dateString: string) => {
        const date = new Date(dateString);
        return date.toLocaleString();
    };

    const getConfidenceColor = (score?: number): "success" | "warning" | "important" | "informative" => {
        if (!score) return 'informative';
        if (score >= 0.8) return 'success';
        if (score >= 0.6) return 'warning';
        return 'important';
    };

    return (
        <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle>
                        Smart Group Members: {groupName}
                    </DialogTitle>
                    <DialogContent>
                        {loading ? (
                            <div style={{ textAlign: 'center', padding: tokens.spacingVerticalXXL }}>
                                <Spinner label="Loading members..." />
                            </div>
                        ) : error ? (
                            <div className={styles.errorMessage}>{error}</div>
                        ) : result ? (
                            <>
                                <div className={styles.cacheInfo}>
                                    {result.fromCache ? (
                                        <>
                                            <CheckmarkCircleRegular />
                                            <Text size={200}>
                                                Cached results from {formatDate(result.resolvedAt)}
                                            </Text>
                                        </>
                                    ) : (
                                        <>
                                            <CloudArrowUpRegular />
                                            <Text size={200}>
                                                Freshly resolved at {formatDate(result.resolvedAt)}
                                            </Text>
                                        </>
                                    )}
                                    <Button
                                        size="small"
                                        appearance="subtle"
                                        icon={<ArrowSyncRegular />}
                                        onClick={handleForceRefresh}
                                        disabled={refreshing}
                                    >
                                        {refreshing ? 'Refreshing...' : 'Force Refresh'}
                                    </Button>
                                </div>

                                {result.members.length === 0 ? (
                                    <div className={styles.emptyState}>
                                        <Text>No members found matching this smart group criteria.</Text>
                                    </div>
                                ) : (
                                    <>
                                        <Text weight="semibold">
                                            {result.members.length} member{result.members.length !== 1 ? 's' : ''} found
                                        </Text>
                                        
                                        <Table className={styles.memberTable}>
                                            <TableHeader>
                                                <TableRow>
                                                    <TableHeaderCell className={styles.wideColumn}>UPN</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Display Name</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Email</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Job Title</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Department</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Office</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>City</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>State</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Country</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Manager</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Company</TableHeaderCell>
                                                    <TableHeaderCell className={styles.narrowColumn}>Confidence</TableHeaderCell>
                                                </TableRow>
                                            </TableHeader>
                                            <TableBody>
                                                {result.members.map((member, index) => (
                                                    <TableRow key={index}>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.userPrincipalName}>
                                                                <Text size={200}>{member.userPrincipalName}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.displayName || '-'}>
                                                                <Text size={200}>{member.displayName || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.mail || '-'}>
                                                                <Text size={200}>{member.mail || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.jobTitle || '-'}>
                                                                <Text size={200}>{member.jobTitle || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.department || '-'}>
                                                                <Text size={200}>{member.department || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.officeLocation || '-'}>
                                                                <Text size={200}>{member.officeLocation || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.city || '-'}>
                                                                <Text size={200}>{member.city || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.state || '-'}>
                                                                <Text size={200}>{member.state || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.country || '-'}>
                                                                <Text size={200}>{member.country || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.managerDisplayName || '-'}>
                                                                <Text size={200}>{member.managerDisplayName || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className={styles.truncatedText} title={member.companyName || '-'}>
                                                                <Text size={200}>{member.companyName || '-'}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            {member.confidenceScore ? (
                                                                <div className={styles.confidenceScore}>
                                                                    <Badge appearance="filled" color={getConfidenceColor(member.confidenceScore)}>
                                                                        {(member.confidenceScore * 100).toFixed(0)}%
                                                                    </Badge>
                                                                </div>
                                                            ) : (
                                                                <Text size={200}>-</Text>
                                                            )}
                                                        </TableCell>
                                                    </TableRow>
                                                ))}
                                            </TableBody>
                                        </Table>
                                    </>
                                )}
                            </>
                        ) : null}
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={onClose} icon={<DismissRegular />}>
                            Close
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
