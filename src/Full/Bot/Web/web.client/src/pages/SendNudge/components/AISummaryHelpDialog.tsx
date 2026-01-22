import React from 'react';
import {
    Button,
    Text,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogContent,
    tokens,
} from '@fluentui/react-components';

interface AISummaryHelpDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export const AISummaryHelpDialog: React.FC<AISummaryHelpDialogProps> = ({
    open,
    onOpenChange,
}) => {
    return (
        <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface style={{ maxWidth: '700px', width: '90vw' }}>
                <DialogBody>
                    <DialogTitle>Example User Data for AI Prompts</DialogTitle>
                    <DialogContent>
                        <Text style={{ marginBottom: tokens.spacingVerticalM }}>
                            When writing smart group descriptions, the AI has access to user information in this format. 
                            Use these examples to craft better prompts:
                        </Text>
                        
                        <div style={{ 
                            backgroundColor: tokens.colorNeutralBackground3, 
                            padding: tokens.spacingVerticalM, 
                            borderRadius: tokens.borderRadiusMedium,
                            fontFamily: 'monospace',
                            fontSize: tokens.fontSizeBase200,
                            marginBottom: tokens.spacingVerticalM,
                            overflowX: 'auto'
                        }}>
                            <div style={{ marginBottom: tokens.spacingVerticalM }}>
                                <Text weight="semibold" style={{ color: tokens.colorBrandForeground1 }}>Example User 1:</Text>
                                <div style={{ marginTop: tokens.spacingVerticalXS, whiteSpace: 'pre-wrap' }}>
                                    UPN: john.smith@contoso.com | Name: John Smith | Job Title: Sales Manager | Department: Sales | 
                                    Office: Seattle HQ | City: Seattle | State: Washington | Country: United States | 
                                    Company: Contoso Corp | Manager: Jane Williams | Employee Type: Full-time | Has Copilot License: Yes | 
                                    Copilot Activity: [Overall: 2024-01-15, Chat: 2024-01-14, Teams: 2024-01-15, Word: 2024-01-12, 
                                    Excel: 2024-01-10, PowerPoint: 2024-01-08, Outlook: 2024-01-15]
                                </div>
                            </div>
                            
                            <div style={{ marginBottom: tokens.spacingVerticalM }}>
                                <Text weight="semibold" style={{ color: tokens.colorBrandForeground1 }}>Example User 2:</Text>
                                <div style={{ marginTop: tokens.spacingVerticalXS, whiteSpace: 'pre-wrap' }}>
                                    UPN: sarah.johnson@contoso.com | Name: Sarah Johnson | Job Title: Software Engineer | Department: Engineering | 
                                    Office: New York Office | City: New York | State: New York | Country: United States | 
                                    Company: Contoso Corp | Manager: Michael Chen | Employee Type: Full-time | Has Copilot License: Yes | 
                                    Copilot Activity: [Overall: 2024-01-16, Chat: 2024-01-16, Teams: 2024-01-15]
                                </div>
                            </div>
                            
                            <div>
                                <Text weight="semibold" style={{ color: tokens.colorBrandForeground1 }}>Example User 3:</Text>
                                <div style={{ marginTop: tokens.spacingVerticalXS, whiteSpace: 'pre-wrap' }}>
                                    UPN: mike.davis@contoso.com | Name: Mike Davis | Job Title: Marketing Coordinator | Department: Marketing | 
                                    Office: Austin Branch | City: Austin | State: Texas | Country: United States | 
                                    Company: Contoso Corp | Employee Type: Contractor | Has Copilot License: No
                                </div>
                            </div>
                        </div>
                        
                        <div style={{ 
                            backgroundColor: tokens.colorNeutralBackground2, 
                            padding: tokens.spacingVerticalM, 
                            borderRadius: tokens.borderRadiusMedium,
                            marginTop: tokens.spacingVerticalM
                        }}>
                            <Text weight="semibold" style={{ display: 'block', marginBottom: tokens.spacingVerticalXS }}>
                                Example Smart Group Descriptions:
                            </Text>
                            <ul style={{ margin: 0, paddingLeft: tokens.spacingHorizontalL }}>
                                <li style={{ marginBottom: tokens.spacingVerticalXS }}>
                                    <Text size={200}>"All employees in the Sales department based in the United States"</Text>
                                </li>
                                <li style={{ marginBottom: tokens.spacingVerticalXS }}>
                                    <Text size={200}>"Software Engineers who have used Copilot in the last 7 days"</Text>
                                </li>
                                <li style={{ marginBottom: tokens.spacingVerticalXS }}>
                                    <Text size={200}>"Users with a Copilot license who haven't used it in the last 30 days"</Text>
                                </li>
                                <li style={{ marginBottom: tokens.spacingVerticalXS }}>
                                    <Text size={200}>"Employees without a Copilot license in the Engineering department"</Text>
                                </li>
                                <li style={{ marginBottom: tokens.spacingVerticalXS }}>
                                    <Text size={200}>"All licensed Copilot users who are heavy users of Teams Copilot"</Text>
                                </li>
                                <li>
                                    <Text size={200}>"Full-time employees with Copilot licenses who report to Jane Williams"</Text>
                                </li>
                            </ul>
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="primary" onClick={() => onOpenChange(false)}>
                            Got it
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
