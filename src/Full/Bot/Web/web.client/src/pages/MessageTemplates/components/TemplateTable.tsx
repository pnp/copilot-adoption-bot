import React from 'react';
import {
    Button,
    Card,
    CardHeader,
    Table,
    TableBody,
    TableCell,
    TableRow,
    TableHeader,
    TableHeaderCell,
    TableCellLayout,
    Text,
    makeStyles,
    tokens
} from '@fluentui/react-components';
import { Edit20Regular, Delete20Regular, Eye20Regular } from '@fluentui/react-icons';
import { MessageTemplateDto } from '../../../apimodels/Models';

const useStyles = makeStyles({
    card: {
        marginBottom: tokens.spacingVerticalL,
    },
    actionButtons: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
});

interface TemplateTableProps {
    templates: MessageTemplateDto[];
    onView: (template: MessageTemplateDto) => void;
    onEdit: (template: MessageTemplateDto) => void;
    onDelete: (template: MessageTemplateDto) => void;
}

export const TemplateTable: React.FC<TemplateTableProps> = ({
    templates,
    onView,
    onEdit,
    onDelete,
}) => {
    const styles = useStyles();

    return (
        <Card className={styles.card}>
            <CardHeader header={<Text weight="semibold">Templates</Text>} />
            <Table>
                <TableHeader>
                    <TableRow>
                        <TableHeaderCell>Name</TableHeaderCell>
                        <TableHeaderCell>Created By</TableHeaderCell>
                        <TableHeaderCell>Created Date</TableHeaderCell>
                        <TableHeaderCell>Actions</TableHeaderCell>
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {templates.map((template) => (
                        <TableRow key={template.id}>
                            <TableCell>
                                <TableCellLayout>{template.templateName}</TableCellLayout>
                            </TableCell>
                            <TableCell>
                                <TableCellLayout>{template.createdByUpn}</TableCellLayout>
                            </TableCell>
                            <TableCell>
                                <TableCellLayout>
                                    {new Date(template.createdDate).toLocaleDateString()}
                                </TableCellLayout>
                            </TableCell>
                            <TableCell>
                                <TableCellLayout>
                                    <div className={styles.actionButtons}>
                                        <Button
                                            size="small"
                                            icon={<Eye20Regular />}
                                            onClick={() => onView(template)}
                                        />
                                        <Button
                                            size="small"
                                            icon={<Edit20Regular />}
                                            onClick={() => onEdit(template)}
                                        />
                                        <Button
                                            size="small"
                                            icon={<Delete20Regular />}
                                            onClick={() => onDelete(template)}
                                        />
                                    </div>
                                </TableCellLayout>
                            </TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </Card>
    );
};
