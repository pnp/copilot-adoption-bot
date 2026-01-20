import React from 'react';
import {
    Button,
    Input,
    Label,
    Text,
    Textarea,
    Dialog,
    DialogTrigger,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { Search20Regular } from '@fluentui/react-icons';
import { SmartGroupMemberDto } from '../../../apimodels/Models';

const useStyles = makeStyles({
    formField: {
        marginBottom: tokens.spacingVerticalM,
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

interface CreateSmartGroupDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    name: string;
    setName: (name: string) => void;
    description: string;
    setDescription: (description: string) => void;
    previewMembers: SmartGroupMemberDto[];
    hasPreviewedCreate: boolean;
    previewing: boolean;
    creating: boolean;
    onPreview: () => void;
    onCreate: () => void;
}

export const CreateSmartGroupDialog: React.FC<CreateSmartGroupDialogProps> = ({
    open,
    onOpenChange,
    name,
    setName,
    description,
    setDescription,
    previewMembers,
    hasPreviewedCreate,
    previewing,
    creating,
    onPreview,
    onCreate,
}) => {
    const styles = useStyles();

    return (
        <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
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
                                value={name}
                                onChange={(_, data) => setName(data.value)}
                                style={{ width: '100%' }}
                            />
                        </div>
                        <div className={styles.formField}>
                            <Label htmlFor="smartGroupDescription" required>Description</Label>
                            <Textarea
                                id="smartGroupDescription"
                                placeholder="Describe the users you want to target. e.g., 'All employees in the Sales department based in the United States who report to the VP of Sales'"
                                value={description}
                                onChange={(_, data) => setDescription(data.value)}
                                rows={6}
                                style={{ width: '100%' }}
                                resize="vertical"
                            />
                        </div>
                        <Button
                            size="small"
                            icon={<Search20Regular />}
                            onClick={onPreview}
                            disabled={!description.trim() || previewing}
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
                            onClick={onCreate}
                            disabled={!name.trim() || !description.trim() || creating}
                        >
                            {creating ? 'Creating...' : 'Create'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
