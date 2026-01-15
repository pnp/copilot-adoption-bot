import React from 'react';
import {
    Button,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
    Text
} from '@fluentui/react-components';

interface TemplateDeleteDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    templateName: string;
    onConfirm: () => void;
}

export const TemplateDeleteDialog: React.FC<TemplateDeleteDialogProps> = ({
    open,
    onOpenChange,
    templateName,
    onConfirm,
}) => {
    const handleClose = () => {
        onOpenChange(false);
    };

    return (
        <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>Confirm Delete</DialogTitle>
                    <DialogContent>
                        <Text>
                            Are you sure you want to delete the template "{templateName}"?
                            This action cannot be undone.
                        </Text>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={handleClose}>
                            Cancel
                        </Button>
                        <Button appearance="primary" onClick={onConfirm}>
                            Delete
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
