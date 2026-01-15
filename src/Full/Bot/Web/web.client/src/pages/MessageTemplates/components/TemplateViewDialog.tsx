import React, { useState, useCallback, useRef } from 'react';
import {
    Button,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
    makeStyles,
    tokens,
    Text
} from '@fluentui/react-components';
import { AdaptiveCard } from '../../../components/common/controls/AdaptiveCard';

const useStyles = makeStyles({
    dialogContent: {
        padding: 0,
        overflow: 'visible',
    },
    contentContainer: {
        display: 'flex',
        height: '500px',
        position: 'relative',
        width: '100%',
    },
    jsonSection: {
        display: 'grid',
        gridTemplateRows: 'auto 1fr',
        minWidth: '200px',
        height: '500px',
    },
    resizer: {
        width: '8px',
        height: '500px',
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
        display: 'grid',
        gridTemplateRows: 'auto 1fr',
        height: '500px',
    },
    previewSection: {
        overflow: 'auto',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        minHeight: 0,
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
    jsonPreWrapper: {
        overflow: 'auto',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        minHeight: 0,
    },
    jsonPre: {
        whiteSpace: 'pre-wrap',
        wordWrap: 'break-word',
        margin: 0,
        fontFamily: 'monospace',
        fontSize: tokens.fontSizeBase200,
    },
    sectionLabel: {
        marginBottom: tokens.spacingVerticalS,
        display: 'block',
        fontWeight: tokens.fontWeightSemibold,
    },
    dialogSurface: {
        maxWidth: '1100px',
        width: '90vw',
    },
});

interface TemplateViewDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    templateName: string;
    json: string;
}

export const TemplateViewDialog: React.FC<TemplateViewDialogProps> = ({
    open,
    onOpenChange,
    templateName,
    json,
}) => {
    const styles = useStyles();
    const containerRef = useRef<HTMLDivElement>(null);
    const [leftPanelWidth, setLeftPanelWidth] = useState<number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const isValidJson = React.useMemo(() => {
        if (!json.trim()) return false;
        try {
            JSON.parse(json);
            return true;
        } catch {
            return false;
        }
    }, [json]);

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
            const maxWidth = containerWidth - 200 - 8;
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

    return (
        <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle>{templateName}</DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <div className={styles.contentContainer} ref={containerRef}>
                            <div 
                                className={styles.jsonSection}
                                style={{ width: leftPanelWidth ?? '50%', flexShrink: 0 }}
                            >
                                <Text className={styles.sectionLabel}>JSON Payload</Text>
                                <div className={styles.jsonPreWrapper} style={{ maxHeight: 'none' }}>
                                    <pre className={styles.jsonPre} style={{ maxHeight: 'none', maxWidth: 'none' }}>
                                        {json}
                                    </pre>
                                </div>
                            </div>
                            <div 
                                className={`${styles.resizer} ${isDragging ? styles.resizerActive : ''}`}
                                onMouseDown={handleMouseDown}
                            />
                            <div className={styles.previewWrapper}>
                                <Text className={styles.sectionLabel}>Adaptive Card Preview</Text>
                                <div className={styles.previewSection}>
                                    {isValidJson ? (
                                        <AdaptiveCard json={json} />
                                    ) : (
                                        <Text>Invalid JSON - cannot render preview</Text>
                                    )}
                                </div>
                            </div>
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={handleClose}>
                            Close
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
