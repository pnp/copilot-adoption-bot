import React from 'react';
import {
    Button,
    Card,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { ArrowRight20Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
    successCard: {
        padding: tokens.spacingVerticalL,
        backgroundColor: tokens.colorPaletteGreenBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginTop: tokens.spacingVerticalM,
        marginBottom: tokens.spacingVerticalL,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        marginTop: tokens.spacingVerticalM,
    },
});

interface SuccessMessageCardProps {
    message: string;
    batchId?: string;
    onViewBatch?: () => void;
}

export const SuccessMessageCard: React.FC<SuccessMessageCardProps> = ({
    message,
    batchId,
    onViewBatch,
}) => {
    const styles = useStyles();

    if (batchId && onViewBatch) {
        return (
            <Card className={styles.successCard}>
                <div className={styles.success}>
                    <Text weight="semibold" size={400}>{message}</Text>
                </div>
                <div style={{ marginTop: tokens.spacingVerticalM }}>
                    <Button
                        appearance="primary"
                        icon={<ArrowRight20Regular />}
                        onClick={onViewBatch}
                    >
                        View Batch Progress
                    </Button>
                </div>
            </Card>
        );
    }

    return <div className={styles.success}>{message}</div>;
};
