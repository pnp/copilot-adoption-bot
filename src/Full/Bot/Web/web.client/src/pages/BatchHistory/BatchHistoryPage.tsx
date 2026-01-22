import React, { useState, useEffect } from 'react';
import {
    Button,
    Card,
    CardHeader,
    Spinner,
    Text,
    makeStyles,
    tokens,
    Table,
    TableBody,
    TableCell,
    TableRow,
    TableHeader,
    TableHeaderCell,
    TableCellLayout,
    Dialog,
    DialogTrigger,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
} from '@fluentui/react-components';
import { Eye20Regular, Copy20Regular, Delete20Regular } from '@fluentui/react-icons';
import { useHistory } from 'react-router-dom';
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import { getAllBatches, getAllTemplates, getMessageLogsByBatch, deleteBatch } from '../../api/ApiCalls';
import { MessageBatchDto, MessageTemplateDto } from '../../apimodels/Models';

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
    error: {
        color: tokens.colorPaletteRedForeground1,
        marginTop: tokens.spacingVerticalM,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        marginTop: tokens.spacingVerticalM,
    },
    actionButtons: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
    truncatedText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '200px',
        display: 'block',
    },
    emptyState: {
        padding: tokens.spacingVerticalXXL,
        textAlign: 'center',
    },
});

interface BatchHistoryPageProps {
    loader?: BaseAxiosApiLoader;
}

export const BatchHistoryPage: React.FC<BatchHistoryPageProps> = ({ loader }) => {
    const styles = useStyles();
    const history = useHistory();

    const [batches, setBatches] = useState<MessageBatchDto[]>([]);
    const [templates, setTemplates] = useState<Map<string, MessageTemplateDto>>(new Map());
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);
    const [copyingBatchId, setCopyingBatchId] = useState<string | null>(null);
    const [deletingBatchId, setDeletingBatchId] = useState<string | null>(null);
    const [batchToDelete, setBatchToDelete] = useState<MessageBatchDto | null>(null);

    useEffect(() => {
        loadData();
    }, [loader]);

    const loadData = async () => {
        if (!loader) return;

        try {
            setLoading(true);
            setError(null);

            const [batchesData, templatesData] = await Promise.all([
                getAllBatches(loader),
                getAllTemplates(loader),
            ]);

            // Sort batches by created date (newest first)
            batchesData.sort((a, b) => 
                new Date(b.createdDate).getTime() - new Date(a.createdDate).getTime()
            );

            setBatches(batchesData);

            // Create a map of templates for quick lookup
            const templateMap = new Map<string, MessageTemplateDto>();
            templatesData.forEach(t => templateMap.set(t.id, t));
            setTemplates(templateMap);
        } catch (err: any) {
            setError(err.message || 'Failed to load batch history');
            console.error('Error loading batch history:', err);
        } finally {
            setLoading(false);
        }
    };

    const handleViewBatch = (batchId: string) => {
        history.push(`/batch/${batchId}`);
    };

    const handleCopyBatch = async (batch: MessageBatchDto) => {
        if (!loader) return;
        
        try {
            setCopyingBatchId(batch.id);
            setError(null);
            setSuccess(null);

            // Get the message logs to extract recipients
            const logs = await getMessageLogsByBatch(loader, batch.id);
            const recipients = logs
                .map(log => log.recipientUpn)
                .filter((upn): upn is string => upn !== undefined && upn !== null);

            // Navigate to SendNudge page with pre-populated data
            history.push('/sendnudge', {
                copyFromBatch: {
                    batchName: `${batch.batchName} (Copy)`,
                    templateId: batch.templateId,
                    recipientUpns: recipients,
                },
            });
        } catch (err: any) {
            setError(err.message || 'Failed to copy batch');
            console.error('Error copying batch:', err);
        } finally {
            setCopyingBatchId(null);
        }
    };

    const handleDeleteBatch = async () => {
        if (!loader || !batchToDelete) return;
        
        try {
            setDeletingBatchId(batchToDelete.id);
            setError(null);
            setSuccess(null);

            await deleteBatch(loader, batchToDelete.id);
            
            setSuccess(`Successfully deleted batch "${batchToDelete.batchName}"`);
            setBatchToDelete(null);
            
            // Reload the batches list
            await loadData();
        } catch (err: any) {
            setError(err.message || 'Failed to delete batch');
            console.error('Error deleting batch:', err);
        } finally {
            setDeletingBatchId(null);
        }
    };

    if (loading) {
        return (
            <div className={styles.container}>
                <Spinner label="Loading batch history..." />
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <h1>Batch History</h1>
                <Text>View all message batches and their progress</Text>
            </div>

            {error && <div className={styles.error}>{error}</div>}
            {success && <div className={styles.success}>{success}</div>}

            <Card className={styles.card}>
                <CardHeader header={<Text weight="semibold">All Batches ({batches.length})</Text>} />
                
                {batches.length === 0 ? (
                    <div className={styles.emptyState}>
                        <Text size={400}>No batches found</Text>
                        <br />
                        <Text>Create your first batch from the Send Nudge page</Text>
                    </div>
                ) : (
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>Batch Name</TableHeaderCell>
                                <TableHeaderCell>Template</TableHeaderCell>
                                <TableHeaderCell>Sender</TableHeaderCell>
                                <TableHeaderCell>Created Date</TableHeaderCell>
                                <TableHeaderCell>Actions</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {batches.map((batch) => {
                                const template = templates.get(batch.templateId);
                                return (
                                    <TableRow key={batch.id}>
                                        <TableCell>
                                            <TableCellLayout>{batch.batchName}</TableCellLayout>
                                        </TableCell>
                                        <TableCell>
                                            <TableCellLayout>
                                                {template?.templateName || batch.templateId}
                                            </TableCellLayout>
                                        </TableCell>
                                        <TableCell>
                                            <TableCellLayout>
                                                <div className={styles.truncatedText} title={batch.senderUpn}>
                                                    {batch.senderUpn}
                                                </div>
                                            </TableCellLayout>
                                        </TableCell>
                                        <TableCell>
                                            <TableCellLayout>
                                                {new Date(batch.createdDate).toLocaleString()}
                                            </TableCellLayout>
                                        </TableCell>
                                        <TableCell>
                                            <TableCellLayout>
                                                <div className={styles.actionButtons}>
                                                    <Button
                                                        size="small"
                                                        icon={<Eye20Regular />}
                                                        onClick={() => handleViewBatch(batch.id)}
                                                        title="View Progress"
                                                    />
                                                    <Button
                                                        size="small"
                                                        icon={<Copy20Regular />}
                                                        onClick={() => handleCopyBatch(batch)}
                                                        disabled={copyingBatchId === batch.id}
                                                        title="Copy Batch"
                                                    />
                                                    <Dialog>
                                                        <DialogTrigger disableButtonEnhancement>
                                                            <Button
                                                                size="small"
                                                                icon={<Delete20Regular />}
                                                                onClick={() => setBatchToDelete(batch)}
                                                                disabled={deletingBatchId === batch.id}
                                                                title="Delete Batch"
                                                            />
                                                        </DialogTrigger>
                                                        <DialogSurface>
                                                            <DialogBody>
                                                                <DialogTitle>Delete Batch</DialogTitle>
                                                                <DialogContent>
                                                                    Are you sure you want to delete the batch "{batch.batchName}"? 
                                                                    This will also delete all associated message logs. This action cannot be undone.
                                                                </DialogContent>
                                                                <DialogActions>
                                                                    <DialogTrigger disableButtonEnhancement>
                                                                        <Button appearance="secondary">Cancel</Button>
                                                                    </DialogTrigger>
                                                                    <Button 
                                                                        appearance="primary" 
                                                                        onClick={handleDeleteBatch}
                                                                        disabled={deletingBatchId === batch.id}
                                                                    >
                                                                        {deletingBatchId === batch.id ? 'Deleting...' : 'Delete'}
                                                                    </Button>
                                                                </DialogActions>
                                                            </DialogBody>
                                                        </DialogSurface>
                                                    </Dialog>
                                                </div>
                                            </TableCellLayout>
                                        </TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                )}
            </Card>
        </div>
    );
};
