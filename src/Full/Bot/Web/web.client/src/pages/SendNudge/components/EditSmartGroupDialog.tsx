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

interface EditSmartGroupDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    name: string;
    setName: (name: string) => void;
    description: string;
    setDescription: (description: string) => void;
    previewMembers: SmartGroupMemberDto[];
    hasPreviewedEdit: boolean;
    previewing: boolean;
    updating: boolean;
    onPreview: () => void;
    onUpdate: () => void;
}

export const EditSmartGroupDialog: React.FC<EditSmartGroupDialogProps> = ({
    open,
    onOpenChange,
    name,
    setName,
    description,
    setDescription,
    previewMembers,
    hasPreviewedEdit,
    previewing,
    updating,
    onPreview,
    onUpdate,
}) => {
    const styles = useStyles();

    return (
        <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface style={{ maxWidth: '600px', width: '90vw' }}>
                <DialogBody>
                    <DialogTitle>Edit Smart Group</DialogTitle>
                    <DialogContent>
                        <div className={styles.formField} style={{ marginTop: tokens.spacingVerticalM }}>
                            <Label htmlFor="editSmartGroupName" required>Group Name</Label>
                            <Input
                                id="editSmartGroupName"
                                placeholder="e.g., Sales Team US"
                                value={name}
                                onChange={(_, data) => setName(data.value)}
                                style={{ width: '100%' }}
                            />
                        </div>
                        <div className={styles.formField}>
                            <Label htmlFor="editSmartGroupDescription" required>Description</Label>
                            <Textarea
                                id="editSmartGroupDescription"
                                placeholder="Describe the users you want to target."
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
                        {hasPreviewedEdit && (
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
                            onClick={onUpdate}
                            disabled={!name.trim() || !description.trim() || updating}
                        >
                            {updating ? 'Updating...' : 'Update'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
