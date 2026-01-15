import React, { useMemo, useState, useCallback, useRef } from 'react';
import {
    Button,
    Dialog,
    DialogTrigger,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
    Input,
    Label,
    Textarea,
    Text,
    makeStyles,
    tokens
} from '@fluentui/react-components';
import { Add20Regular } from '@fluentui/react-icons';
import { AdaptiveCard } from '../../../components/common/controls/AdaptiveCard';

const useStyles = makeStyles({
    formField: {
        marginBottom: tokens.spacingVerticalM,
        display: 'flex',
        flexDirection: 'column',
    },
    jsonTextareaWrapper: {
        display: 'flex',
        flexDirection: 'column',
        flexGrow: 1,
        minHeight: 0,
    },
    jsonTextarea: {
        flexGrow: 1,
        display: 'flex',
        flexDirection: 'column',
        fontFamily: 'monospace',
        fontSize: tokens.fontSizeBase200,
        '& > textarea': {
            flexGrow: 1,
            minHeight: '350px',
        },
    },
    contentContainer: {
        display: 'flex',
        height: '500px',
        position: 'relative',
    },
    formSection: {
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        minWidth: '200px',
        overflow: 'hidden',
    },
    resizer: {
        width: '8px',
        cursor: 'col-resize',
        backgroundColor: 'transparent',
        position: 'relative',
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        ':hover': {
            '::after': {
                backgroundColor: tokens.colorBrandBackground,
            },
        },
        '::after': {
            content: '""',
            position: 'absolute',
            top: '0',
            bottom: '0',
            width: '2px',
            backgroundColor: tokens.colorNeutralStroke1,
            borderRadius: '1px',
            transition: 'background-color 0.15s ease',
        },
    },
    resizerActive: {
        '::after': {
            backgroundColor: tokens.colorBrandBackground,
        },
    },
    previewWrapper: {
        flex: 1,
        minWidth: '200px',
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        overflow: 'hidden',
    },
    previewSection: {
        flexGrow: 1,
        overflow: 'auto',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        minHeight: 0,
        // Target all adaptive card elements to ensure full width
        '& > div': {
            width: '100%',
        },
        '& > div > div': {
            width: '100%',
        },
        '& .adaptiveCardContainer': {
            width: '100%',
        },
        '& .adaptiveCardContainer > div': {
            width: '100%',
        },
    },
    previewLabel: {
        marginBottom: tokens.spacingVerticalS,
        display: 'block',
        flexShrink: 0,
    },
    errorText: {
        color: tokens.colorPaletteRedForeground1,
        fontSize: tokens.fontSizeBase200,
        marginTop: tokens.spacingVerticalXS,
        flexShrink: 0,
    },
    dialogSurface: {
        maxWidth: '1100px',
        width: '90vw',
    },
});

interface TemplateFormDialogProps {
    mode: 'create' | 'edit';
    open: boolean;
    onOpenChange: (open: boolean) => void;
    templateName: string;
    onTemplateNameChange: (name: string) => void;
    jsonPayload: string;
    onJsonPayloadChange: (json: string) => void;
    onSubmit: () => void;
    triggerButton?: boolean;
}

export const TemplateFormDialog: React.FC<TemplateFormDialogProps> = ({
    mode,
    open,
    onOpenChange,
    templateName,
    onTemplateNameChange,
    jsonPayload,
    onJsonPayloadChange,
    onSubmit,
    triggerButton = false,
}) => {
    const styles = useStyles();
    const containerRef = useRef<HTMLDivElement>(null);
    const [leftPanelWidth, setLeftPanelWidth] = useState<number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const jsonValidation = useMemo(() => {
        if (!jsonPayload.trim()) {
            return { isValid: false, error: null };
        }
        try {
            JSON.parse(jsonPayload);
            return { isValid: true, error: null };
        } catch (e) {
            return { isValid: false, error: (e as Error).message };
        }
    }, [jsonPayload]);

    const isCreateMode = mode === 'create';
    const title = isCreateMode ? 'Create New Template' : 'Edit Template';
    const submitLabel = isCreateMode ? 'Create' : 'Save';

    const handleClose = () => {
        onOpenChange(false);
    };

    const handleMouseDown = useCallback((e: React.MouseEvent) => {
        e.preventDefault();
        setIsDragging(true);

        const startX = e.clientX;
        const startWidth = leftPanelWidth ?? (containerRef.current?.offsetWidth ?? 800) / 2;

        const handleMouseMove = (moveEvent: MouseEvent) => {
            const containerWidth = containerRef.current?.offsetWidth ?? 800;
            const minWidth = 200;
            const maxWidth = containerWidth - 200 - 8; // 8px for resizer
            const deltaX = moveEvent.clientX - startX;
            const newWidth = Math.min(Math.max(startWidth + deltaX, minWidth), maxWidth);
            setLeftPanelWidth(newWidth);
        };

        const handleMouseUp = () => {
            setIsDragging(false);
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
        };

        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);
    }, [leftPanelWidth]);

    const dialogContent = (
        <DialogSurface className={styles.dialogSurface}>
            <DialogBody>
                <DialogTitle>{title}</DialogTitle>
                <DialogContent>
                    <div className={styles.contentContainer} ref={containerRef}>
                        <div 
                            className={styles.formSection}
                            style={{ width: leftPanelWidth ?? '50%', flexShrink: 0 }}
                        >
                            <div className={styles.formField}>
                                <Label htmlFor={`${mode}TemplateName`}>Template Name</Label>
                                <Input
                                    id={`${mode}TemplateName`}
                                    value={templateName}
                                    onChange={(_, data) => onTemplateNameChange(data.value)}
                                />
                            </div>
                            <div className={styles.jsonTextareaWrapper}>
                                <Label htmlFor={`${mode}JsonPayload`}>JSON Payload</Label>
                                <Textarea
                                    id={`${mode}JsonPayload`}
                                    className={styles.jsonTextarea}
                                    value={jsonPayload}
                                    onChange={(_, data) => onJsonPayloadChange(data.value)}
                                    resize="vertical"
                                />
                                {jsonValidation.error && (
                                    <Text className={styles.errorText}>
                                        Invalid JSON: {jsonValidation.error}
                                    </Text>
                                )}
                            </div>
                        </div>
                        <div 
                            className={`${styles.resizer} ${isDragging ? styles.resizerActive : ''}`}
                            onMouseDown={handleMouseDown}
                        />
                        <div className={styles.previewWrapper}>
                            <Label className={styles.previewLabel}>Adaptive Card Preview</Label>
                            <div className={styles.previewSection}>
                                {jsonValidation.isValid ? (
                                    <AdaptiveCard json={jsonPayload} />
                                ) : (
                                    <Text>
                                        {jsonPayload.trim() 
                                            ? 'Fix JSON errors to see preview' 
                                            : 'Enter valid JSON to see preview'}
                                    </Text>
                                )}
                            </div>
                        </div>
                    </div>
                </DialogContent>
                <DialogActions>
                    <Button appearance="secondary" onClick={handleClose}>
                        Cancel
                    </Button>
                    <Button appearance="primary" onClick={onSubmit}>
                        {submitLabel}
                    </Button>
                </DialogActions>
            </DialogBody>
        </DialogSurface>
    );

    if (triggerButton) {
        return (
            <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
                <DialogTrigger disableButtonEnhancement>
                    <Button appearance="primary" icon={<Add20Regular />}>
                        New Template
                    </Button>
                </DialogTrigger>
                {dialogContent}
            </Dialog>
        );
    }

    return (
        <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
            {dialogContent}
        </Dialog>
    );
};
