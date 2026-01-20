import React from 'react';
import {
    Card,
    CardHeader,
    Input,
    Label,
    Text,
    Dropdown,
    Option,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { MessageTemplateDto } from '../../../apimodels/Models';

const useStyles = makeStyles({
    card: {
        marginBottom: tokens.spacingVerticalL,
    },
    formField: {
        marginBottom: tokens.spacingVerticalM,
    },
    summaryCard: {
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        marginTop: tokens.spacingVerticalM,
    },
});

interface BatchDetailsSectionProps {
    batchName: string;
    setBatchName: (name: string) => void;
    templates: MessageTemplateDto[];
    selectedTemplateId: string;
    setSelectedTemplateId: (id: string) => void;
}

export const BatchDetailsSection: React.FC<BatchDetailsSectionProps> = ({
    batchName,
    setBatchName,
    templates,
    selectedTemplateId,
    setSelectedTemplateId,
}) => {
    const styles = useStyles();
    const selectedTemplate = templates.find(t => t.id === selectedTemplateId);

    return (
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
    );
};
