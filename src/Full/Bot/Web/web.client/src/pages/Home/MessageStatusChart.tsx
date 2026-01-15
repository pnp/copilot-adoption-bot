import { Pie } from 'react-chartjs-2';
import React from 'react';
import { ColourPicker } from '../../utils/ColourPicker';
import { MessageStatusStatsDto } from '../../apimodels/Models';

interface MessageStatusChartProps {
  stats: MessageStatusStatsDto;
}

export const MessageStatusChart: React.FC<MessageStatusChartProps> = ({ stats }) => {
  const data = {
    labels: ['Sent', 'Failed', 'Pending'],
    datasets: [
      {
        data: [stats.sentCount, stats.failedCount, stats.pendingCount],
        backgroundColor: [
          ColourPicker.chartColours[0], // Green for sent
          ColourPicker.chartColours[1], // Red for failed
          ColourPicker.chartColours[2]  // Yellow for pending
        ],
        borderColor: ['#002050'],
        borderWidth: 1,
      }
    ],
  };

  return (
    <div>
      <Pie 
        data={data} 
        options={{ 
          plugins: { 
            legend: { display: false } 
          }, 
          maintainAspectRatio: false 
        }} 
      />
    </div>
  );
};
