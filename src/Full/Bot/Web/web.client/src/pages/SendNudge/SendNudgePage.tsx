import React, { useState, useEffect, useRef } from 'react';
import {
    Button,
    Card,
    CardHeader,
    Input,
    Label,
    Spinner,
    Text,
    makeStyles,
    tokens,
    Dropdown,
    Option,
    Badge,
    Checkbox,
    Textarea,
    Dialog,
    DialogTrigger,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
} from '@fluentui/react-components';
import {
    DocumentAdd20Regular,
    Delete20Regular,
    Send20Regular,
    AddCircle20Regular,
    ArrowRight20Regular,
    Info20Regular,
    Sparkle20Regular,
    PeopleTeam20Regular,
    Search20Regular,
    Edit20Regular,
} from '@fluentui/react-icons';
import { useHistory, useLocation } from 'react-router-dom';
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import {
    getAllTemplates,
    parseFile,
    createBatchAndSend,
    getCopilotConnectedStatus,
    getAllSmartGroups,
    createSmartGroup,
    updateSmartGroup,
    deleteSmartGroup,
    previewSmartGroup,
} from '../../api/ApiCalls';
import { MessageTemplateDto, CreateBatchAndSendRequest, CopilotConnectedStatusDto, SmartGroupDto, SmartGroupMemberDto } from '../../apimodels/Models';

const useStyles = makeStyles({
    container: {
        padding: tokens.spacingVerticalXXL,
    },
    header: {
        marginBottom: tokens.spacingVerticalL,
    },
    card: {
        marginBottom: tokens.spacingVerticalL,
    },
    formField: {
        marginBottom: tokens.spacingVerticalM,
    },
    fileUploadArea: {
        border: `2px dashed ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalXL,
        textAlign: 'center',
        cursor: 'pointer',
        backgroundColor: tokens.colorNeutralBackground3,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    upnListContainer: {
        marginTop: tokens.spacingVerticalM,
        maxHeight: '300px',
        overflowY: 'auto',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalM,
    },
    upnItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: tokens.spacingVerticalS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    addUpnContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'flex-end',
    },
    buttonContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        marginTop: tokens.spacingVerticalL,
    },
    error: {
        color: tokens.colorPaletteRedForeground1,
        marginTop: tokens.spacingVerticalM,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        marginTop: tokens.spacingVerticalM,
    },
    successCard: {
        padding: tokens.spacingVerticalL,
        backgroundColor: tokens.colorPaletteGreenBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginTop: tokens.spacingVerticalM,
        marginBottom: tokens.spacingVerticalL,
    },
    hiddenInput: {
        display: 'none',
    },
    summaryCard: {
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginTop: tokens.spacingVerticalM,
    },
    infoCard: {
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorBrandBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginBottom: tokens.spacingVerticalL,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    copilotConnectedBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
        backgroundColor: tokens.colorBrandBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginBottom: tokens.spacingVerticalM,
    },
    smartGroupSection: {
        marginTop: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorBrandStroke1}`,
    },
    smartGroupItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusSmall,
        marginBottom: tokens.spacingVerticalXS,
    },
    smartGroupInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    previewContainer: {
        marginTop: tokens.spacingVerticalM,
        maxHeight: '200px',
        overflowY: 'auto',
    },
    previewItem: {
        padding: tokens.spacingVerticalXS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        fontSize: tokens.fontSizeBase200,
    },
});

interface SendNudgePageProps {
    loader?: BaseAxiosApiLoader;
}

interface LocationState {
    copyFromBatch?: {
        batchName: string;
        templateId: string;
        recipientUpns: string[];
    };
}

export const SendNudgePage: React.FC<SendNudgePageProps> = ({ loader }) => {
const styles = useStyles();
const history = useHistory();
const location = useLocation<LocationState>();
const fileInputRef = useRef<HTMLInputElement>(null);

const [templates, setTemplates] = useState<MessageTemplateDto[]>([]);
const [loading, setLoading] = useState(true);
const [error, setError] = useState<string | null>(null);
const [success, setSuccess] = useState<string | null>(null);
const [createdBatchId, setCreatedBatchId] = useState<string | null>(null);

// Form states
const [batchName, setBatchName] = useState('');
const [selectedTemplateId, setSelectedTemplateId] = useState<string>('');
const [recipientUpns, setRecipientUpns] = useState<string[]>([]);
const [newUpn, setNewUpn] = useState('');
const [uploadedFileName, setUploadedFileName] = useState<string>('');
const [sending, setSending] = useState(false);
const [isCopiedBatch, setIsCopiedBatch] = useState(false);

// Copilot Connected / Smart Group states
const [copilotConnectedStatus, setCopilotConnectedStatus] = useState<CopilotConnectedStatusDto | null>(null);
const [smartGroups, setSmartGroups] = useState<SmartGroupDto[]>([]);
const [selectedSmartGroupIds, setSelectedSmartGroupIds] = useState<string[]>([]);
const [showCreateSmartGroupDialog, setShowCreateSmartGroupDialog] = useState(false);
const [newSmartGroupName, setNewSmartGroupName] = useState('');
const [newSmartGroupDescription, setNewSmartGroupDescription] = useState('');
const [creatingSmartGroup, setCreatingSmartGroup] = useState(false);
const [previewMembers, setPreviewMembers] = useState<SmartGroupMemberDto[]>([]);
const [previewing, setPreviewing] = useState(false);
const [hasPreviewedCreate, setHasPreviewedCreate] = useState(false);

// Edit smart group states
const [editingSmartGroup, setEditingSmartGroup] = useState<SmartGroupDto | null>(null);
const [showEditSmartGroupDialog, setShowEditSmartGroupDialog] = useState(false);
const [editSmartGroupName, setEditSmartGroupName] = useState('');
const [editSmartGroupDescription, setEditSmartGroupDescription] = useState('');
const [updatingSmartGroup, setUpdatingSmartGroup] = useState(false);
const [deletingSmartGroupId, setDeletingSmartGroupId] = useState<string | null>(null);
const [editPreviewMembers, setEditPreviewMembers] = useState<SmartGroupMemberDto[]>([]);
const [editPreviewing, setEditPreviewing] = useState(false);
const [hasPreviewedEdit, setHasPreviewedEdit] = useState(false);

useEffect(() => {
    loadTemplates();
    loadCopilotConnectedStatus();
}, [loader]);

useEffect(() => {
    // Check if we're copying from an existing batch
    if (location.state?.copyFromBatch) {
        const { batchName: copiedBatchName, templateId, recipientUpns: copiedRecipients } = location.state.copyFromBatch;
        setBatchName(copiedBatchName);
        setSelectedTemplateId(templateId);
        setRecipientUpns(copiedRecipients);
        setIsCopiedBatch(true);
            
        // Clear the location state to prevent re-populating on refresh
        window.history.replaceState({}, document.title);
    }
}, [location.state]);

const loadCopilotConnectedStatus = async () => {
    if (!loader) return;

    try {
        const status = await getCopilotConnectedStatus(loader);
        setCopilotConnectedStatus(status);
            
        if (status.isEnabled) {
            loadSmartGroups();
        }
    } catch (err: any) {
        console.error('Error loading Copilot Connected status:', err);
        // Don't show error - just means smart groups won't be available
    }
};

const loadSmartGroups = async () => {
    if (!loader) return;

    try {
        const groups = await getAllSmartGroups(loader);
        setSmartGroups(groups);
    } catch (err: any) {
        console.error('Error loading smart groups:', err);
    }
};

    const loadTemplates = async () => {
        if (!loader) return;

        try {
            setLoading(true);
            setError(null);
            const data = await getAllTemplates(loader);
            setTemplates(data);
        } catch (err: any) {
            setError(err.message || 'Failed to load templates');
            console.error('Error loading templates:', err);
        } finally {
            setLoading(false);
        }
    };

    const handleSmartGroupToggle = (groupId: string, checked: boolean) => {
        if (checked) {
            setSelectedSmartGroupIds([...selectedSmartGroupIds, groupId]);
        } else {
            setSelectedSmartGroupIds(selectedSmartGroupIds.filter(id => id !== groupId));
        }
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
            setSelectedSmartGroupIds(selectedSmartGroupIds.filter(id => id !== groupId));
            setSuccess(`Smart group "${group.name}" deleted successfully!`);
        } catch (err: any) {
            setError(err.message || 'Failed to delete smart group');
            console.error('Error deleting smart group:', err);
        } finally {
            setDeletingSmartGroupId(null);
        }
    };

    const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file || !loader) return;

        try {
            setError(null);
            setLoading(true);
            const response = await parseFile(loader, file);
            setRecipientUpns(response.upns);
            setUploadedFileName(file.name);
            setSuccess(`Loaded ${response.upns.length} recipients from ${file.name}`);
            setCreatedBatchId(null);
            setIsCopiedBatch(false);
        } catch (err: any) {
            setError(err.message || 'Failed to parse file');
            console.error('Error parsing file:', err);
        } finally {
            setLoading(false);
        }
    };

    const handleAddUpn = () => {
        if (!newUpn.trim()) return;

        if (recipientUpns.includes(newUpn.trim())) {
            setError('This UPN is already in the list');
            return;
        }

        setRecipientUpns([...recipientUpns, newUpn.trim()]);
        setNewUpn('');
        setError(null);
    };

    const handleRemoveUpn = (upn: string) => {
        setRecipientUpns(recipientUpns.filter(u => u !== upn));
    };

    const handleFileUploadClick = () => {
        fileInputRef.current?.click();
    };

    const handleSendNudges = async () => {
        if (!loader) return;

        // Validation
        if (!batchName.trim()) {
            setError('Batch name is required');
            return;
        }

        if (!selectedTemplateId) {
            setError('Please select a template');
            return;
        }

        const hasRecipients = recipientUpns.length > 0;
        const hasSmartGroups = selectedSmartGroupIds.length > 0;

        if (!hasRecipients && !hasSmartGroups) {
            setError('Please add at least one recipient or select a smart group');
            return;
        }

        try {
            setError(null);
            setSuccess(null);
            setCreatedBatchId(null);
            setSending(true);

            const request: CreateBatchAndSendRequest = {
                batchName: batchName.trim(),
                templateId: selectedTemplateId,
                recipientUpns: recipientUpns.length > 0 ? recipientUpns : undefined,
                smartGroupIds: selectedSmartGroupIds.length > 0 ? selectedSmartGroupIds : undefined,
            };

            const response = await createBatchAndSend(loader, request);
            
            const smartGroupNote = response.smartGroupsResolved > 0 
                ? ` (including ${response.smartGroupsResolved} smart group(s))` 
                : '';
            
            setSuccess(`Successfully created batch "${batchName}" with ${response.messageCount} messages${smartGroupNote}!`);
            setCreatedBatchId(response.batch.id);
            
            // Reset form
            setBatchName('');
            setSelectedTemplateId('');
            setRecipientUpns([]);
            setSelectedSmartGroupIds([]);
            setUploadedFileName('');
            setIsCopiedBatch(false);
        } catch (err: any) {
            setError(err.message || 'Failed to send nudges');
            console.error('Error sending nudges:', err);
        } finally {
            setSending(false);
        }
    };

    const handleViewBatchProgress = () => {
        if (createdBatchId) {
            history.push(`/batch/${createdBatchId}`);
        }
    };

    const handleKeyPress = (event: React.KeyboardEvent) => {
        if (event.key === 'Enter') {
            handleAddUpn();
        }
    };

    if (loading && templates.length === 0) {
        return <Spinner label="Loading templates..." />;
    }

    const selectedTemplate = templates.find(t => t.id === selectedTemplateId);

    return (
    <div className={styles.container}>
        <div className={styles.header}>
            <h1>Send Nudge</h1>
            <Text>Select a template and add recipients to send nudge messages</Text>
                
            {copilotConnectedStatus?.isEnabled && (
                <div className={styles.copilotConnectedBadge}>
                    <Sparkle20Regular />
                    <Badge appearance="filled" color="brand">Copilot Connected</Badge>
                    <Text size={200}>AI-powered smart groups available</Text>
                </div>
            )}
        </div>

        {isCopiedBatch && (
            <div className={styles.infoCard}>
                <Info20Regular />
                <Text>
                    You're creating a new batch from an existing batch. The template and recipients have been pre-populated. You can modify them before sending.
                </Text>
            </div>
        )}

        {error && <div className={styles.error}>{error}</div>}
            
        {success && createdBatchId && (
            <Card className={styles.successCard}>
                <div className={styles.success}>
                    <Text weight="semibold" size={400}>{success}</Text>
                </div>
                <div style={{ marginTop: tokens.spacingVerticalM }}>
                    <Button
                        appearance="primary"
                        icon={<ArrowRight20Regular />}
                        onClick={handleViewBatchProgress}
                    >
                        View Batch Progress
                    </Button>
                </div>
            </Card>
        )}
            
        {success && !createdBatchId && <div className={styles.success}>{success}</div>}

            <Card className={styles.card}>
                <CardHeader header={<Text weight="semibold">Batch Details</Text>} />
                
                <div className={styles.formField}>
                    <Label htmlFor="batchName" required>Batch Name</Label>
                    <Input
                        id="batchName"
                        placeholder="e.g., Q1 2024 Reminder"
                        value={batchName}
                        onChange={(_, data) => setBatchName(data.value)}
                    />
                </div>

                <div className={styles.formField}>
                    <Label htmlFor="template" required>Select Template</Label>
                    <Dropdown
                        id="template"
                        placeholder="Select a message template"
                        value={selectedTemplate?.templateName || ''}
                        onOptionSelect={(_, data) => setSelectedTemplateId(data.optionValue as string)}
                    >
                        {templates.map((template) => (
                            <Option key={template.id} value={template.id}>
                                {template.templateName}
                            </Option>
                        ))}
                    </Dropdown>
                </div>

                {selectedTemplate && (
                    <div className={styles.summaryCard}>
                        <Text size={200}>
                            <strong>Selected Template:</strong> {selectedTemplate.templateName}
                        </Text>
                        <br />
                        <Text size={200}>
                            <strong>Created by:</strong> {selectedTemplate.createdByUpn}
                        </Text>
                    </div>
                )}
            </Card>

            <Card className={styles.card}>
            <CardHeader header={<Text weight="semibold">Recipients ({recipientUpns.length} direct{selectedSmartGroupIds.length > 0 ? ` + ${selectedSmartGroupIds.length} smart group(s)` : ''})</Text>} />

            {/* Smart Groups Section - Only shown when Copilot Connected is enabled */}
            {copilotConnectedStatus?.isEnabled && (
                <div className={styles.smartGroupSection}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: tokens.spacingVerticalS }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                            <Sparkle20Regular />
                            <Text weight="semibold">Smart Groups</Text>
                        </div>
                        <Dialog open={showCreateSmartGroupDialog} onOpenChange={(_, data) => setShowCreateSmartGroupDialog(data.open)}>
                            <DialogTrigger disableButtonEnhancement>
                                <Button size="small" icon={<AddCircle20Regular />}>
                                    Create Smart Group
                                </Button>
                            </DialogTrigger>
                            <DialogSurface style={{ maxWidth: '600px', width: '90vw' }}>
                                <DialogBody>
                                    <DialogTitle>Create Smart Group</DialogTitle>
                                    <DialogContent>
                                        <Text>
                                            Smart groups use AI to find users matching your description.
                                            Describe the target audience and AI will identify matching users.
                                        </Text>
                                        <div className={styles.formField} style={{ marginTop: tokens.spacingVerticalM }}>
                                            <Label htmlFor="smartGroupName" required>Group Name</Label>
                                            <Input
                                                id="smartGroupName"
                                                placeholder="e.g., Sales Team US"
                                                value={newSmartGroupName}
                                                onChange={(_, data) => setNewSmartGroupName(data.value)}
                                                style={{ width: '100%' }}
                                            />
                                        </div>
                                        <div className={styles.formField}>
                                            <Label htmlFor="smartGroupDescription" required>Description</Label>
                                            <Textarea
                                                id="smartGroupDescription"
                                                placeholder="Describe the users you want to target. e.g., 'All employees in the Sales department based in the United States who report to the VP of Sales'"
                                                value={newSmartGroupDescription}
                                                onChange={(_, data) => setNewSmartGroupDescription(data.value)}
                                                rows={6}
                                                style={{ width: '100%' }}
                                                resize="vertical"
                                            />
                                        </div>
                                        <Button
                                            size="small"
                                            icon={<Search20Regular />}
                                            onClick={handlePreviewSmartGroup}
                                            disabled={!newSmartGroupDescription.trim() || previewing}
                                        >
                                            {previewing ? 'Previewing...' : 'Preview Matches'}
                                        </Button>
                                        {hasPreviewedCreate && (
                                            <div className={styles.previewContainer}>
                                                {previewMembers.length > 0 ? (
                                                    <>
                                                        <Text size={200} weight="semibold">Preview: {previewMembers.length} users matched</Text>
                                                        {previewMembers.slice(0, 10).map((member, idx) => (
                                                            <div key={idx} className={styles.previewItem}>
                                                                <Text size={200}>{member.displayName || member.userPrincipalName}</Text>
                                                                {member.department && <Text size={100}> - {member.department}</Text>}
                                                                {member.confidenceScore && <Text size={100}> ({(member.confidenceScore * 100).toFixed(0)}% match)</Text>}
                                                            </div>
                                                        ))}
                                                        {previewMembers.length > 10 && (
                                                            <Text size={200}>...and {previewMembers.length - 10} more</Text>
                                                        )}
                                                    </>
                                                ) : (
                                                    <Text size={200} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                                                        No users matched your description. Try being more specific or using different criteria.
                                                    </Text>
                                                )}
                                            </div>
                                        )}
                                    </DialogContent>
                                    <DialogActions>
                                        <DialogTrigger disableButtonEnhancement>
                                            <Button appearance="secondary">Cancel</Button>
                                        </DialogTrigger>
                                        <Button
                                            appearance="primary"
                                            onClick={handleCreateSmartGroup}
                                            disabled={!newSmartGroupName.trim() || !newSmartGroupDescription.trim() || creatingSmartGroup}
                                        >
                                            {creatingSmartGroup ? 'Creating...' : 'Create'}
                                        </Button>
                                    </DialogActions>
                                </DialogBody>
                            </DialogSurface>
                        </Dialog>
                    </div>

                    {smartGroups.length === 0 ? (
                        <Text size={200}>No smart groups created yet. Create one to use AI-powered user targeting.</Text>
                    ) : (
                        smartGroups.map(group => (
                            <div key={group.id} className={styles.smartGroupItem}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                                    <Checkbox
                                        checked={selectedSmartGroupIds.includes(group.id)}
                                        onChange={(_, data) => handleSmartGroupToggle(group.id, data.checked as boolean)}
                                    />
                                    <div className={styles.smartGroupInfo}>
                                        <Text weight="semibold">{group.name}</Text>
                                        <Text size={200}>{group.description}</Text>
                                    </div>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                                    <PeopleTeam20Regular />
                                    <Text size={200}>
                                        {group.lastResolvedMemberCount != null 
                                            ? `${group.lastResolvedMemberCount} members` 
                                            : 'Not resolved yet'}
                                    </Text>
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
                            </div>
                        ))
                    )}

                    {/* Edit Smart Group Dialog */}
                    <Dialog open={showEditSmartGroupDialog} onOpenChange={(_, data) => setShowEditSmartGroupDialog(data.open)}>
                        <DialogSurface style={{ maxWidth: '600px', width: '90vw' }}>
                            <DialogBody>
                                <DialogTitle>Edit Smart Group</DialogTitle>
                                <DialogContent>
                                    <div className={styles.formField} style={{ marginTop: tokens.spacingVerticalM }}>
                                        <Label htmlFor="editSmartGroupName" required>Group Name</Label>
                                        <Input
                                            id="editSmartGroupName"
                                            placeholder="e.g., Sales Team US"
                                            value={editSmartGroupName}
                                            onChange={(_, data) => setEditSmartGroupName(data.value)}
                                            style={{ width: '100%' }}
                                        />
                                    </div>
                                    <div className={styles.formField}>
                                        <Label htmlFor="editSmartGroupDescription" required>Description</Label>
                                        <Textarea
                                            id="editSmartGroupDescription"
                                            placeholder="Describe the users you want to target."
                                            value={editSmartGroupDescription}
                                            onChange={(_, data) => setEditSmartGroupDescription(data.value)}
                                            rows={6}
                                            style={{ width: '100%' }}
                                            resize="vertical"
                                        />
                                    </div>
                                    <Button
                                        size="small"
                                        icon={<Search20Regular />}
                                        onClick={handlePreviewEditSmartGroup}
                                        disabled={!editSmartGroupDescription.trim() || editPreviewing}
                                    >
                                        {editPreviewing ? 'Previewing...' : 'Preview Matches'}
                                    </Button>
                                    {hasPreviewedEdit && (
                                        <div className={styles.previewContainer}>
                                            {editPreviewMembers.length > 0 ? (
                                                <>
                                                    <Text size={200} weight="semibold">Preview: {editPreviewMembers.length} users matched</Text>
                                                    {editPreviewMembers.slice(0, 10).map((member, idx) => (
                                                        <div key={idx} className={styles.previewItem}>
                                                            <Text size={200}>{member.displayName || member.userPrincipalName}</Text>
                                                            {member.department && <Text size={100}> - {member.department}</Text>}
                                                            {member.confidenceScore && <Text size={100}> ({(member.confidenceScore * 100).toFixed(0)}% match)</Text>}
                                                        </div>
                                                    ))}
                                                    {editPreviewMembers.length > 10 && (
                                                        <Text size={200}>...and {editPreviewMembers.length - 10} more</Text>
                                                    )}
                                                </>
                                            ) : (
                                                <Text size={200} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                                                    No users matched your description. Try being more specific or using different criteria.
                                                </Text>
                                            )}
                                        </div>
                                    )}
                                    <div style={{ marginTop: tokens.spacingVerticalM }}>
                                        <Text size={200}>
                                            Note: Updating the description will require re-resolving the group members when used.
                                        </Text>
                                    </div>
                                </DialogContent>
                                <DialogActions>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary">Cancel</Button>
                                    </DialogTrigger>
                                    <Button
                                        appearance="primary"
                                        onClick={handleUpdateSmartGroup}
                                        disabled={!editSmartGroupName.trim() || !editSmartGroupDescription.trim() || updatingSmartGroup}
                                    >
                                        {updatingSmartGroup ? 'Updating...' : 'Update'}
                                    </Button>
                                </DialogActions>
                            </DialogBody>
                        </DialogSurface>
                    </Dialog>
                </div>
            )}

            <div className={styles.formField} style={{ marginTop: tokens.spacingVerticalM }}>
                <Label>Upload File (CSV or Excel)</Label>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".csv,.xlsx,.xls,.txt"
                        onChange={handleFileUpload}
                        className={styles.hiddenInput}
                    />
                    <div className={styles.fileUploadArea} onClick={handleFileUploadClick}>
                        <DocumentAdd20Regular />
                        <Text>
                            {uploadedFileName
                                ? `Uploaded: ${uploadedFileName}`
                                : 'Click to upload a file with user UPNs (single column)'}
                        </Text>
                    </div>
                </div>

                <div className={styles.formField}>
                    <Label htmlFor="newUpn">Or Add UPN Manually</Label>
                    <div className={styles.addUpnContainer}>
                        <Input
                            id="newUpn"
                            placeholder="user@example.com"
                            value={newUpn}
                            onChange={(_, data) => setNewUpn(data.value)}
                            onKeyPress={handleKeyPress}
                            style={{ flex: 1 }}
                        />
                        <Button
                            icon={<AddCircle20Regular />}
                            onClick={handleAddUpn}
                            disabled={!newUpn.trim()}
                        >
                            Add
                        </Button>
                    </div>
                </div>

                {recipientUpns.length > 0 && (
                    <div className={styles.upnListContainer}>
                        {recipientUpns.map((upn, index) => (
                            <div key={index} className={styles.upnItem}>
                                <Text>{upn}</Text>
                                <Button
                                    size="small"
                                    appearance="subtle"
                                    icon={<Delete20Regular />}
                                    onClick={() => handleRemoveUpn(upn)}
                                />
                            </div>
                        ))}
                    </div>
                )}
            </Card>

            <div className={styles.buttonContainer}>
                <Button
                    appearance="primary"
                    icon={<Send20Regular />}
                    onClick={handleSendNudges}
                    disabled={sending || !batchName || !selectedTemplateId || (recipientUpns.length === 0 && selectedSmartGroupIds.length === 0)}
                >
                    {sending ? 'Sending...' : `Send to ${recipientUpns.length} Recipients${selectedSmartGroupIds.length > 0 ? ` + ${selectedSmartGroupIds.length} Smart Group(s)` : ''}`}
                </Button>
            </div>
        </div>
    );
};
