import React, { useState, useEffect } from 'react';
import {
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
    Sparkle20Regular,
    AddCircle20Regular,
    Edit20Regular,
    Delete20Regular,
    PeopleTeam20Regular,
    Info20Regular,
    ArrowSyncRegular,
} from '@fluentui/react-icons';
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import {
    getAllSmartGroups,
    createSmartGroup,
    updateSmartGroup,
    deleteSmartGroup,
    previewSmartGroup,
    getCopilotConnectedStatus,
    resolveSmartGroupMembers,
} from '../../api/ApiCalls';
import { SmartGroupDto, SmartGroupMemberDto, CopilotConnectedStatusDto } from '../../apimodels/Models';
import { CreateSmartGroupDialog } from '../SendNudge/components/CreateSmartGroupDialog';
import { EditSmartGroupDialog } from '../SendNudge/components/EditSmartGroupDialog';
import { AISummaryHelpDialog } from '../SendNudge/components/AISummaryHelpDialog';
import { SmartGroupMembersDialog } from './components/SmartGroupMembersDialog';

const useStyles = makeStyles({
    container: {
        padding: tokens.spacingVerticalXXL,
    },
    header: {
        marginBottom: tokens.spacingVerticalL,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    headerContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    copilotConnectedBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    error: {
        color: tokens.colorPaletteRedForeground1,
        marginBottom: tokens.spacingVerticalM,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        marginBottom: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorPaletteGreenBackground2,
        borderRadius: tokens.borderRadiusMedium,
    },
    emptyState: {
        textAlign: 'center',
        padding: tokens.spacingVerticalXXL,
        color: tokens.colorNeutralForeground3,
    },
    tableContainer: {
        marginTop: tokens.spacingVerticalM,
        overflowX: 'auto',
    },
    truncatedText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '200px',
        display: 'block',
    },
    actionButtons: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
    },
    memberLink: {
        cursor: 'pointer',
        color: tokens.colorBrandForeground1,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        '&:hover': {
            textDecoration: 'underline',
        },
    },
    notEnabledMessage: {
        padding: tokens.spacingVerticalXXL,
        textAlign: 'center',
    },
    headerActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        alignItems: 'center',
    },
});

interface SmartGroupsPageProps {
    loader?: BaseAxiosApiLoader;
}

export const SmartGroupsPage: React.FC<SmartGroupsPageProps> = ({ loader }) => {
    const styles = useStyles();

    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);
    const [smartGroups, setSmartGroups] = useState<SmartGroupDto[]>([]);
    const [copilotConnectedStatus, setCopilotConnectedStatus] = useState<CopilotConnectedStatusDto | null>(null);

    // Create dialog state
    const [showCreateSmartGroupDialog, setShowCreateSmartGroupDialog] = useState(false);
    const [newSmartGroupName, setNewSmartGroupName] = useState('');
    const [newSmartGroupDescription, setNewSmartGroupDescription] = useState('');
    const [creatingSmartGroup, setCreatingSmartGroup] = useState(false);
    const [previewMembers, setPreviewMembers] = useState<SmartGroupMemberDto[]>([]);
    const [previewing, setPreviewing] = useState(false);
    const [hasPreviewedCreate, setHasPreviewedCreate] = useState(false);

    // Edit dialog state
    const [editingSmartGroup, setEditingSmartGroup] = useState<SmartGroupDto | null>(null);
    const [showEditSmartGroupDialog, setShowEditSmartGroupDialog] = useState(false);
    const [editSmartGroupName, setEditSmartGroupName] = useState('');
    const [editSmartGroupDescription, setEditSmartGroupDescription] = useState('');
    const [updatingSmartGroup, setUpdatingSmartGroup] = useState(false);
    const [editPreviewMembers, setEditPreviewMembers] = useState<SmartGroupMemberDto[]>([]);
    const [editPreviewing, setEditPreviewing] = useState(false);
    const [hasPreviewedEdit, setHasPreviewedEdit] = useState(false);

    // Delete state
    const [deletingSmartGroupId, setDeletingSmartGroupId] = useState<string | null>(null);

    // Help dialog state
    const [showAISummaryHelp, setShowAISummaryHelp] = useState(false);

    // Members dialog state
    const [showMembersDialog, setShowMembersDialog] = useState(false);
    const [selectedGroupForMembers, setSelectedGroupForMembers] = useState<SmartGroupDto | null>(null);
    const [resolvingGroupId, setResolvingGroupId] = useState<string | null>(null);

    useEffect(() => {
        loadCopilotConnectedStatus();
    }, [loader]);

    const loadCopilotConnectedStatus = async () => {
        if (!loader) return;

        try {
            const status = await getCopilotConnectedStatus(loader);
            setCopilotConnectedStatus(status);
            
            if (status.isEnabled) {
                loadSmartGroups();
            } else {
                setLoading(false);
            }
        } catch (err: any) {
            console.error('Error loading Copilot Connected status:', err);
            setError(err.message || 'Failed to load Copilot Connected status');
            setLoading(false);
        }
    };

    const loadSmartGroups = async (isRefresh: boolean = false) => {
        if (!loader) return;

        try {
            if (isRefresh) {
                setRefreshing(true);
            } else {
                setLoading(true);
            }
            setError(null);
            const groups = await getAllSmartGroups(loader);
            setSmartGroups(groups);
        } catch (err: any) {
            console.error('Error loading smart groups:', err);
            setError(err.message || 'Failed to load smart groups');
        } finally {
            setLoading(false);
            setRefreshing(false);
        }
    };

    const handleRefresh = () => {
        loadSmartGroups(true);
    };

    const handleCreateSmartGroup = async () => {
        if (!loader || !newSmartGroupName.trim() || !newSmartGroupDescription.trim()) return;

        try {
            setCreatingSmartGroup(true);
            setError(null);
            
            const newGroup = await createSmartGroup(loader, {
                name: newSmartGroupName.trim(),
                description: newSmartGroupDescription.trim()
            });

            setSmartGroups([...smartGroups, newGroup]);
            setNewSmartGroupName('');
            setNewSmartGroupDescription('');
            setShowCreateSmartGroupDialog(false);
            setSuccess(`Smart group "${newGroup.name}" created successfully!`);
            setPreviewMembers([]);
            setHasPreviewedCreate(false);
            
            // Clear success message after 5 seconds
            setTimeout(() => setSuccess(null), 5000);
        } catch (err: any) {
            setError(err.message || 'Failed to create smart group');
            console.error('Error creating smart group:', err);
        } finally {
            setCreatingSmartGroup(false);
        }
    };

    const handlePreviewSmartGroup = async () => {
        if (!loader || !newSmartGroupDescription.trim()) return;

        try {
            setPreviewing(true);
            setError(null);
            
            const result = await previewSmartGroup(loader, {
                description: newSmartGroupDescription.trim(),
                maxUsers: 50
            });

            setPreviewMembers(result.members);
            setHasPreviewedCreate(true);
        } catch (err: any) {
            setError(err.message || 'Failed to preview smart group');
            console.error('Error previewing smart group:', err);
        } finally {
            setPreviewing(false);
        }
    };

    const handleEditSmartGroup = (group: SmartGroupDto) => {
        setEditingSmartGroup(group);
        setEditSmartGroupName(group.name);
        setEditSmartGroupDescription(group.description);
        setEditPreviewMembers([]);
        setHasPreviewedEdit(false);
        setShowEditSmartGroupDialog(true);
    };

    const handlePreviewEditSmartGroup = async () => {
        if (!loader || !editSmartGroupDescription.trim()) return;

        try {
            setEditPreviewing(true);
            setError(null);
            
            const result = await previewSmartGroup(loader, {
                description: editSmartGroupDescription.trim(),
                maxUsers: 50
            });

            setEditPreviewMembers(result.members);
            setHasPreviewedEdit(true);
        } catch (err: any) {
            setError(err.message || 'Failed to preview smart group');
            console.error('Error previewing smart group:', err);
        } finally {
            setEditPreviewing(false);
        }
    };

    const handleUpdateSmartGroup = async () => {
        if (!loader || !editingSmartGroup || !editSmartGroupName.trim() || !editSmartGroupDescription.trim()) return;

        try {
            setUpdatingSmartGroup(true);
            setError(null);
            
            const updatedGroup = await updateSmartGroup(loader, editingSmartGroup.id, {
                name: editSmartGroupName.trim(),
                description: editSmartGroupDescription.trim()
            });

            setSmartGroups(smartGroups.map(g => g.id === updatedGroup.id ? updatedGroup : g));
            setShowEditSmartGroupDialog(false);
            setEditingSmartGroup(null);
            setSuccess(`Smart group "${updatedGroup.name}" updated successfully!`);
            
            // Clear success message after 5 seconds
            setTimeout(() => setSuccess(null), 5000);
        } catch (err: any) {
            setError(err.message || 'Failed to update smart group');
            console.error('Error updating smart group:', err);
        } finally {
            setUpdatingSmartGroup(false);
        }
    };

    const handleDeleteSmartGroup = async (groupId: string) => {
        if (!loader) return;

        const group = smartGroups.find(g => g.id === groupId);
        if (!group) return;

        if (!window.confirm(`Are you sure you want to delete the smart group "${group.name}"?`)) {
            return;
        }

        try {
            setDeletingSmartGroupId(groupId);
            setError(null);
            
            await deleteSmartGroup(loader, groupId);

            setSmartGroups(smartGroups.filter(g => g.id !== groupId));
            setSuccess(`Smart group "${group.name}" deleted successfully!`);
            
            // Clear success message after 5 seconds
            setTimeout(() => setSuccess(null), 5000);
        } catch (err: any) {
            setError(err.message || 'Failed to delete smart group');
            console.error('Error deleting smart group:', err);
        } finally {
            setDeletingSmartGroupId(null);
        }
    };

    const handleShowMembers = (group: SmartGroupDto) => {
        setSelectedGroupForMembers(group);
        setShowMembersDialog(true);
    };

    const handleResolveGroup = async (group: SmartGroupDto) => {
        if (!loader) return;

        try {
            setResolvingGroupId(group.id);
            setError(null);

            // Call the resolve API with forceRefresh=true to get fresh data
            const result = await resolveSmartGroupMembers(loader, group.id, true);

            // Update the group in the list with the new member count and resolved date
            const updatedGroups = smartGroups.map(g => {
                if (g.id === group.id) {
                    return {
                        ...g,
                        lastResolvedMemberCount: result.members.length,
                        lastResolvedDate: result.resolvedAt
                    };
                }
                return g;
            });
            setSmartGroups(updatedGroups);

            setSuccess(`Smart group "${group.name}" resolved successfully with ${result.members.length} member${result.members.length !== 1 ? 's' : ''}!`);
            
            // Clear success message after 5 seconds
            setTimeout(() => setSuccess(null), 5000);
        } catch (err: any) {
            setError(err.message || 'Failed to resolve smart group');
            console.error('Error resolving smart group:', err);
        } finally {
            setResolvingGroupId(null);
        }
    };

    const formatDate = (dateString?: string) => {
        if (!dateString) return '-';
        const date = new Date(dateString);
        return date.toLocaleDateString();
    };

    if (loading) {
        return (
            <div className={styles.container}>
                <Spinner label="Loading..." />
            </div>
        );
    }

    if (!copilotConnectedStatus?.isEnabled) {
        return (
            <div className={styles.container}>
                <div className={styles.header}>
                    <h1>Smart Groups</h1>
                </div>
                <div className={styles.notEnabledMessage}>
                    <Sparkle20Regular style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalM }} />
                    <h2>Copilot Connected Mode Not Enabled</h2>
                    <Text>
                        Smart Groups require Copilot Connected mode to be enabled with AI Foundry configuration.
                        Please configure AI Foundry in your application settings to use this feature.
                    </Text>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div className={styles.headerContent}>
                    <h1>Smart Groups</h1>
                    <div className={styles.copilotConnectedBadge}>
                        <Sparkle20Regular />
                        <Badge appearance="filled" color="brand">Copilot Connected</Badge>
                        <Text size={200}>AI-powered user targeting</Text>
                    </div>
                </div>
                <div className={styles.headerActions}>
                    <Button
                        size="small"
                        appearance="subtle"
                        icon={<ArrowSyncRegular />}
                        onClick={handleRefresh}
                        disabled={refreshing}
                        title="Refresh smart groups list"
                    >
                        {refreshing ? 'Refreshing...' : 'Refresh'}
                    </Button>
                    <Button
                        size="small"
                        appearance="subtle"
                        icon={<Info20Regular />}
                        onClick={() => setShowAISummaryHelp(true)}
                        title="View example user data for AI prompts"
                    >
                        Help
                    </Button>
                    <Button
                        appearance="primary"
                        icon={<AddCircle20Regular />}
                        onClick={() => setShowCreateSmartGroupDialog(true)}
                    >
                        Create Smart Group
                    </Button>
                </div>
            </div>

            {error && <div className={styles.error}>{error}</div>}
            {success && <div className={styles.success}>{success}</div>}

            {smartGroups.length === 0 ? (
                <div className={styles.emptyState}>
                    <Sparkle20Regular style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalM }} />
                    <h3>No Smart Groups Yet</h3>
                    <Text>Create your first smart group to use AI-powered user targeting for your nudges.</Text>
                    <br /><br />
                    <Button
                        appearance="primary"
                        icon={<AddCircle20Regular />}
                        onClick={() => setShowCreateSmartGroupDialog(true)}
                    >
                        Create Smart Group
                    </Button>
                </div>
            ) : (
                <div className={styles.tableContainer}>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>Name</TableHeaderCell>
                                <TableHeaderCell>Description</TableHeaderCell>
                                <TableHeaderCell>Members</TableHeaderCell>
                                <TableHeaderCell>Last Resolved</TableHeaderCell>
                                <TableHeaderCell>Created By</TableHeaderCell>
                                <TableHeaderCell>Actions</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {smartGroups.map((group) => (
                                <TableRow key={group.id}>
                                    <TableCell>
                                        <Text weight="semibold">{group.name}</Text>
                                    </TableCell>
                                    <TableCell>
                                        <Text size={200}>{group.description}</Text>
                                    </TableCell>
                                    <TableCell>
                                        {group.lastResolvedMemberCount != null ? (
                                            <div
                                                className={styles.memberLink}
                                                onClick={() => handleShowMembers(group)}
                                            >
                                                <PeopleTeam20Regular />
                                                <Text size={200}>
                                                    {group.lastResolvedMemberCount} member{group.lastResolvedMemberCount !== 1 ? 's' : ''}
                                                </Text>
                                            </div>
                                        ) : (
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<ArrowSyncRegular />}
                                                onClick={() => handleResolveGroup(group)}
                                                disabled={resolvingGroupId === group.id}
                                            >
                                                {resolvingGroupId === group.id ? 'Resolving...' : 'Resolve Now'}
                                            </Button>
                                        )}
                                    </TableCell>
                                    <TableCell>
                                        <Text size={200}>{formatDate(group.lastResolvedDate)}</Text>
                                    </TableCell>
                                    <TableCell>
                                        <div className={styles.truncatedText} title={group.createdByUpn}>
                                            <Text size={200}>{group.createdByUpn}</Text>
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div className={styles.actionButtons}>
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<Edit20Regular />}
                                                onClick={() => handleEditSmartGroup(group)}
                                                title="Edit smart group"
                                            />
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<Delete20Regular />}
                                                onClick={() => handleDeleteSmartGroup(group.id)}
                                                disabled={deletingSmartGroupId === group.id}
                                                title="Delete smart group"
                                            />
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}

            {/* Create Smart Group Dialog */}
            <CreateSmartGroupDialog
                open={showCreateSmartGroupDialog}
                onOpenChange={(open) => {
                    setShowCreateSmartGroupDialog(open);
                    if (!open) {
                        setNewSmartGroupName('');
                        setNewSmartGroupDescription('');
                        setPreviewMembers([]);
                        setHasPreviewedCreate(false);
                    }
                }}
                name={newSmartGroupName}
                setName={setNewSmartGroupName}
                description={newSmartGroupDescription}
                setDescription={setNewSmartGroupDescription}
                onCreate={handleCreateSmartGroup}
                creating={creatingSmartGroup}
                onPreview={handlePreviewSmartGroup}
                previewMembers={previewMembers}
                previewing={previewing}
                hasPreviewedCreate={hasPreviewedCreate}
            />

            {/* Edit Smart Group Dialog */}
            <EditSmartGroupDialog
                open={showEditSmartGroupDialog}
                onOpenChange={(open) => {
                    setShowEditSmartGroupDialog(open);
                    if (!open) {
                        setEditingSmartGroup(null);
                    }
                }}
                name={editSmartGroupName}
                setName={setEditSmartGroupName}
                description={editSmartGroupDescription}
                setDescription={setEditSmartGroupDescription}
                onUpdate={handleUpdateSmartGroup}
                updating={updatingSmartGroup}
                onPreview={handlePreviewEditSmartGroup}
                previewMembers={editPreviewMembers}
                previewing={editPreviewing}
                hasPreviewedEdit={hasPreviewedEdit}
            />

            {/* AI Summary Help Dialog */}
            <AISummaryHelpDialog
                open={showAISummaryHelp}
                onOpenChange={(open) => setShowAISummaryHelp(open)}
            />

            {/* Smart Group Members Dialog */}
            {selectedGroupForMembers && loader && (
                <SmartGroupMembersDialog
                    open={showMembersDialog}
                    onClose={() => {
                        setShowMembersDialog(false);
                        setSelectedGroupForMembers(null);
                        // Refresh the groups list to get updated member counts
                        loadSmartGroups(true);
                    }}
                    groupId={selectedGroupForMembers.id}
                    groupName={selectedGroupForMembers.name}
                    loader={loader}
                />
            )}
        </div>
    );
};
