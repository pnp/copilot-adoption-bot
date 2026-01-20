import React, { useState, useEffect } from 'react';
import {
    Button,
    Spinner,
    Text,
    makeStyles,
    tokens,
    Badge,
} from '@fluentui/react-components';
import {
    Send20Regular,
    Info20Regular,
    Sparkle20Regular,
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
import { BatchDetailsSection } from './components/BatchDetailsSection';
import { SmartGroupsSection } from './components/SmartGroupsSection';
import { RecipientsSection } from './components/RecipientsSection';
import { CreateSmartGroupDialog } from './components/CreateSmartGroupDialog';
import { EditSmartGroupDialog } from './components/EditSmartGroupDialog';
import { AISummaryHelpDialog } from './components/AISummaryHelpDialog';
import { SuccessMessageCard } from './components/SuccessMessageCard';

const useStyles = makeStyles({
    container: {
        padding: tokens.spacingVerticalXXL,
    },
    header: {
        marginBottom: tokens.spacingVerticalL,
    },
    error: {
        color: tokens.colorPaletteRedForeground1,
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
    buttonContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        marginTop: tokens.spacingVerticalL,
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
const [showAISummaryHelp, setShowAISummaryHelp] = useState(false);

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
            
            {success && (
                <SuccessMessageCard 
                    message={success}
                    batchId={createdBatchId || undefined}
                    onViewBatch={handleViewBatchProgress}
                />
            )}
            
            <BatchDetailsSection
                batchName={batchName}
                setBatchName={setBatchName}
                templates={templates}
                selectedTemplateId={selectedTemplateId}
                setSelectedTemplateId={setSelectedTemplateId}
            />

            {copilotConnectedStatus?.isEnabled && (
                <>
                    <SmartGroupsSection
                        smartGroups={smartGroups}
                        selectedSmartGroupIds={selectedSmartGroupIds}
                        deletingSmartGroupId={deletingSmartGroupId}
                        onToggle={handleSmartGroupToggle}
                        onEdit={handleEditSmartGroup}
                        onDelete={handleDeleteSmartGroup}
                        onShowHelp={() => setShowAISummaryHelp(true)}
                        onShowCreateDialog={() => setShowCreateSmartGroupDialog(true)}
                    />

                    <CreateSmartGroupDialog
                        open={showCreateSmartGroupDialog}
                        onOpenChange={setShowCreateSmartGroupDialog}
                        name={newSmartGroupName}
                        setName={setNewSmartGroupName}
                        description={newSmartGroupDescription}
                        setDescription={setNewSmartGroupDescription}
                        previewMembers={previewMembers}
                        hasPreviewedCreate={hasPreviewedCreate}
                        previewing={previewing}
                        creating={creatingSmartGroup}
                        onPreview={handlePreviewSmartGroup}
                        onCreate={handleCreateSmartGroup}
                    />

                    <EditSmartGroupDialog
                        open={showEditSmartGroupDialog}
                        onOpenChange={setShowEditSmartGroupDialog}
                        name={editSmartGroupName}
                        setName={setEditSmartGroupName}
                        description={editSmartGroupDescription}
                        setDescription={setEditSmartGroupDescription}
                        previewMembers={editPreviewMembers}
                        hasPreviewedEdit={hasPreviewedEdit}
                        previewing={editPreviewing}
                        updating={updatingSmartGroup}
                        onPreview={handlePreviewEditSmartGroup}
                        onUpdate={handleUpdateSmartGroup}
                    />

                    <AISummaryHelpDialog
                        open={showAISummaryHelp}
                        onOpenChange={setShowAISummaryHelp}
                    />
                </>
            )}

            <RecipientsSection
                recipientUpns={recipientUpns}
                selectedSmartGroupCount={selectedSmartGroupIds.length}
                newUpn={newUpn}
                setNewUpn={setNewUpn}
                uploadedFileName={uploadedFileName}
                onFileUpload={handleFileUpload}
                onAddUpn={handleAddUpn}
                onRemoveUpn={handleRemoveUpn}
                onKeyPress={handleKeyPress}
            />

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
