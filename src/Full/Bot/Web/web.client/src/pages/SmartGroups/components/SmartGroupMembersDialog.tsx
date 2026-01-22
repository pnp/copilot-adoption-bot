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
    TableCellLayout,
} from '@fluentui/react-components';
import {
    ArrowSyncRegular,
    DismissRegular,
    CheckmarkCircleRegular,
    CloudArrowUpRegular,
    ChevronDownRegular,
    ChevronRightRegular,
} from '@fluentui/react-icons';
import { BaseAxiosApiLoader } from '../../../api/AxiosApiLoader';
import { resolveSmartGroupMembers } from '../../../api/ApiCalls';
import { SmartGroupResolutionResult } from '../../../apimodels/Models';

const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '900px',
        width: '90vw',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
    },
    memberTable: {
        marginTop: tokens.spacingVerticalM,
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
    expandButton: {
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        color: tokens.colorBrandForeground1,
        '&:hover': {
            textDecoration: 'underline',
        },
    },
    expandedRow: {
        backgroundColor: tokens.colorNeutralBackground2,
    },
    detailsContainer: {
        padding: tokens.spacingVerticalM,
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
        gap: tokens.spacingVerticalM,
    },
    detailSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    detailLabel: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    detailValue: {
        fontSize: tokens.fontSizeBase300,
    },
    sectionTitle: {
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalXS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    truncatedText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '200px',
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
    const [expandedRows, setExpandedRows] = useState<Set<number>>(new Set());

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

    const toggleRow = (index: number) => {
        const newExpanded = new Set(expandedRows);
        if (newExpanded.has(index)) {
            newExpanded.delete(index);
        } else {
            newExpanded.add(index);
        }
        setExpandedRows(newExpanded);
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
                                                    <TableHeaderCell style={{ width: '40px' }}></TableHeaderCell>
                                                    <TableHeaderCell>Display Name</TableHeaderCell>
                                                    <TableHeaderCell>UPN</TableHeaderCell>
                                                    <TableHeaderCell>Department</TableHeaderCell>
                                                    <TableHeaderCell>Job Title</TableHeaderCell>
                                                    <TableHeaderCell>Copilot License</TableHeaderCell>
                                                    <TableHeaderCell>Confidence</TableHeaderCell>
                                                </TableRow>
                                            </TableHeader>
                                            <TableBody>
                                                {result.members.map((member, index) => {
                                                    const isExpanded = expandedRows.has(index);
                                                    return (
                                                        <React.Fragment key={index}>
                                                            <TableRow>
                                                                <TableCell>
                                                                    <Button
                                                                        appearance="subtle"
                                                                        size="small"
                                                                        icon={isExpanded ? <ChevronDownRegular /> : <ChevronRightRegular />}
                                                                        onClick={() => toggleRow(index)}
                                                                        aria-label={isExpanded ? 'Collapse' : 'Expand'}
                                                                    />
                                                                </TableCell>
                                                                <TableCell>
                                                                    <TableCellLayout>
                                                                        <Text weight="semibold">{member.displayName || '-'}</Text>
                                                                    </TableCellLayout>
                                                                </TableCell>
                                                                <TableCell>
                                                                    <div className={styles.truncatedText} title={member.userPrincipalName}>
                                                                        <Text size={200}>{member.userPrincipalName}</Text>
                                                                    </div>
                                                                </TableCell>
                                                                <TableCell>
                                                                    <Text size={200}>{member.department || '-'}</Text>
                                                                </TableCell>
                                                                <TableCell>
                                                                    <Text size={200}>{member.jobTitle || '-'}</Text>
                                                                </TableCell>
                                                                <TableCell>
                                                                    {member.hasCopilotLicense ? (
                                                                        <Badge appearance="filled" color="success">
                                                                            Licensed
                                                                        </Badge>
                                                                    ) : (
                                                                        <Badge appearance="tint" color="warning">No License</Badge>
                                                                    )}
                                                                </TableCell>
                                                                <TableCell>
                                                                    {member.confidenceScore ? (
                                                                        <Badge appearance="filled" color={getConfidenceColor(member.confidenceScore)}>
                                                                            {(member.confidenceScore * 100).toFixed(0)}%
                                                                        </Badge>
                                                                    ) : (
                                                                        <Text size={200}>-</Text>
                                                                    )}
                                                                </TableCell>
                                                            </TableRow>
                                                            {isExpanded && (
                                                                <TableRow className={styles.expandedRow}>
                                                                    <TableCell colSpan={7}>
                                                                        <div className={styles.detailsContainer}>
                                                                            {/* Personal Information */}
                                                                            <div>
                                                                                <div className={styles.sectionTitle}>Personal Information</div>
                                                                                <div className={styles.detailSection}>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Given Name</div>
                                                                                        <div className={styles.detailValue}>{member.givenName || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Surname</div>
                                                                                        <div className={styles.detailValue}>{member.surname || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Email</div>
                                                                                        <div className={styles.detailValue}>{member.mail || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Employee Type</div>
                                                                                        <div className={styles.detailValue}>{member.employeeType || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Copilot License</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.hasCopilotLicense ? (
                                                                                                <Badge appearance="filled" color="success">Licensed</Badge>
                                                                                            ) : (
                                                                                                <Badge appearance="tint" color="warning">No License</Badge>
                                                                                            )}
                                                                                        </div>
                                                                                    </div>
                                                                                </div>
                                                                            </div>

                                                                            {/* Location Information */}
                                                                            <div>
                                                                                <div className={styles.sectionTitle}>Location</div>
                                                                                <div className={styles.detailSection}>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Office</div>
                                                                                        <div className={styles.detailValue}>{member.officeLocation || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>City</div>
                                                                                        <div className={styles.detailValue}>{member.city || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>State</div>
                                                                                        <div className={styles.detailValue}>{member.state || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Country</div>
                                                                                        <div className={styles.detailValue}>{member.country || '-'}</div>
                                                                                    </div>
                                                                                </div>
                                                                            </div>

                                                                            {/* Organization Information */}
                                                                            <div>
                                                                                <div className={styles.sectionTitle}>Organization</div>
                                                                                <div className={styles.detailSection}>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Company</div>
                                                                                        <div className={styles.detailValue}>{member.companyName || '-'}</div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Manager</div>
                                                                                        <div className={styles.detailValue}>{member.managerDisplayName || '-'}</div>
                                                                                    </div>
                                                                                    {member.managerUpn && (
                                                                                        <div>
                                                                                            <div className={styles.detailLabel}>Manager UPN</div>
                                                                                            <div className={styles.detailValue}>{member.managerUpn}</div>
                                                                                        </div>
                                                                                    )}
                                                                                    {member.hireDate && (
                                                                                        <div>
                                                                                            <div className={styles.detailLabel}>Hire Date</div>
                                                                                            <div className={styles.detailValue}>
                                                                                                {new Date(member.hireDate).toLocaleDateString()}
                                                                                            </div>
                                                                                        </div>
                                                                                    )}
                                                                                </div>
                                                                            </div>

                                                                            {/* Copilot Usage Statistics */}
                                                                            <div>
                                                                                <div className={styles.sectionTitle}>Copilot Usage</div>
                                                                                <div className={styles.detailSection}>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Overall Activity</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.copilotLastActivityDate 
                                                                                                ? new Date(member.copilotLastActivityDate).toLocaleDateString()
                                                                                                : 'No activity'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Copilot Chat</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.copilotChatLastActivityDate 
                                                                                                ? new Date(member.copilotChatLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Teams</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.teamsCopilotLastActivityDate 
                                                                                                ? new Date(member.teamsCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Word</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.wordCopilotLastActivityDate 
                                                                                                ? new Date(member.wordCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Excel</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.excelCopilotLastActivityDate 
                                                                                                ? new Date(member.excelCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>PowerPoint</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.powerPointCopilotLastActivityDate 
                                                                                                ? new Date(member.powerPointCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Outlook</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.outlookCopilotLastActivityDate 
                                                                                                ? new Date(member.outlookCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>OneNote</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.oneNoteCopilotLastActivityDate 
                                                                                                ? new Date(member.oneNoteCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    <div>
                                                                                        <div className={styles.detailLabel}>Loop</div>
                                                                                        <div className={styles.detailValue}>
                                                                                            {member.loopCopilotLastActivityDate 
                                                                                                ? new Date(member.loopCopilotLastActivityDate).toLocaleDateString()
                                                                                                : '-'}
                                                                                        </div>
                                                                                    </div>
                                                                                    {member.lastCopilotStatsUpdate && (
                                                                                        <div>
                                                                                            <div className={styles.detailLabel}>Stats Updated</div>
                                                                                            <div className={styles.detailValue}>
                                                                                                {new Date(member.lastCopilotStatsUpdate).toLocaleDateString()}
                                                                                            </div>
                                                                                        </div>
                                                                                    )}
                                                                                </div>
                                                                            </div>
                                                                        </div>
                                                                    </TableCell>
                                                                </TableRow>
                                                            )}
                                                        </React.Fragment>
                                                    );
                                                })}
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
