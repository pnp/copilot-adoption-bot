import { Pie } from 'react-chartjs-2';
import React from 'react';
import { ColourPicker } from '../../utils/ColourPicker';
import { UserCoverageStatsDto } from '../../apimodels/Models';

interface UserCoverageChartProps {
  stats: UserCoverageStatsDto;
}

export const UserCoverageChart: React.FC<UserCoverageChartProps> = ({ stats }) => {
  const data = {
    labels: ['Users Messaged', 'Users Not Messaged'],
    datasets: [
      {
        data: [stats.usersMessaged, stats.usersNotMessaged],
        backgroundColor: [
          ColourPicker.chartColours[0], // Users messaged
          ColourPicker.chartColours[3]  // Users not messaged
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
