import React, { useRef } from 'react';
import {
    Button,
    Card,
    CardHeader,
    Input,
    Label,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import {
    DocumentAdd20Regular,
    Delete20Regular,
    AddCircle20Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
    card: {
        marginBottom: tokens.spacingVerticalL,
    },
    formField: {
        marginBottom: tokens.spacingVerticalM,
    },
    fileUploadArea: {
        border: `2px dashed ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalXL,
        textAlign: 'center',
        cursor: 'pointer',
        backgroundColor: tokens.colorNeutralBackground3,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    upnListContainer: {
        marginTop: tokens.spacingVerticalM,
        maxHeight: '300px',
        overflowY: 'auto',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalM,
    },
    upnItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: tokens.spacingVerticalS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    addUpnContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'flex-end',
    },
    hiddenInput: {
        display: 'none',
    },
});

interface RecipientsSectionProps {
    recipientUpns: string[];
    selectedSmartGroupCount: number;
    newUpn: string;
    setNewUpn: (upn: string) => void;
    uploadedFileName: string;
    onFileUpload: (event: React.ChangeEvent<HTMLInputElement>) => void;
    onAddUpn: () => void;
    onRemoveUpn: (upn: string) => void;
    onKeyPress: (event: React.KeyboardEvent) => void;
}

export const RecipientsSection: React.FC<RecipientsSectionProps> = ({
    recipientUpns,
    selectedSmartGroupCount,
    newUpn,
    setNewUpn,
    uploadedFileName,
    onFileUpload,
    onAddUpn,
    onRemoveUpn,
    onKeyPress,
}) => {
    const styles = useStyles();
    const fileInputRef = useRef<HTMLInputElement>(null);

    const handleFileUploadClick = () => {
        fileInputRef.current?.click();
    };

    return (
        <Card className={styles.card}>
            <CardHeader 
                header={
                    <Text weight="semibold">
                        Recipients ({recipientUpns.length} direct{selectedSmartGroupCount > 0 ? ` + ${selectedSmartGroupCount} smart group(s)` : ''})
                    </Text>
                } 
            />

            <div className={styles.formField}>
                <Label>Upload File (CSV or Excel)</Label>
                <input
                    ref={fileInputRef}
                    type="file"
                    accept=".csv,.xlsx,.xls,.txt"
                    onChange={onFileUpload}
                    className={styles.hiddenInput}
                />
                <div className={styles.fileUploadArea} onClick={handleFileUploadClick}>
                    <DocumentAdd20Regular />
                    <Text>
                        {uploadedFileName
                            ? `Uploaded: ${uploadedFileName}`
                            : 'Click to upload a file with user UPNs (single column)'}
                    </Text>
                </div>
            </div>

            <div className={styles.formField}>
                <Label htmlFor="newUpn">Or Add UPN Manually</Label>
                <div className={styles.addUpnContainer}>
                    <Input
                        id="newUpn"
                        placeholder="user@example.com"
                        value={newUpn}
                        onChange={(_, data) => setNewUpn(data.value)}
                        onKeyPress={onKeyPress}
                        style={{ flex: 1 }}
                    />
                    <Button
                        icon={<AddCircle20Regular />}
                        onClick={onAddUpn}
                        disabled={!newUpn.trim()}
                    >
                        Add
                    </Button>
                </div>
            </div>

            {recipientUpns.length > 0 && (
                <div className={styles.upnListContainer}>
                    {recipientUpns.map((upn, index) => (
                        <div key={index} className={styles.upnItem}>
                            <Text>{upn}</Text>
                            <Button
                                size="small"
                                appearance="subtle"
                                icon={<Delete20Regular />}
                                onClick={() => onRemoveUpn(upn)}
                            />
                        </div>
                    ))}
                </div>
            )}
        </Card>
    );
};
