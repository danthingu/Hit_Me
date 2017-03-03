using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Hit_Me
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            blockTiming.Tick += blockTimerDrop;
        }

        private void canvasBackground_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RectangleGeometry rect = new RectangleGeometry();
            rect.Rect = new Rect(0, 0, canvasBackground.ActualWidth, canvasBackground.ActualHeight);
            canvasBackground.Clip = rect;
        }


        private DateTime lastAdjustmentTime = DateTime.MinValue;
        private int droppedCount = 0;
        private int savedCount = 0;

        private int maxDropped = 5;
        private double secondsBetweenAdjustments = 15;

        private double initialSecondsBetweenBlocks = 1.3;
        private double initialSecondsToFall = 3.5;
        private double secondsBetweenBlocks;
        private double secondsToFall;

        private double secondsBetweenBlocksReduction = 0.1;
        private double secondsToFallReduction = 0.1;

        private Dictionary<Block, Storyboard> storyboards = new Dictionary<Block, Storyboard>();

        private DispatcherTimer blockTiming = new DispatcherTimer();

        private void cmdStart_Click(object sender, RoutedEventArgs e)
        {
            cmdStart.IsEnabled = false;
            droppedCount = 0;
            savedCount = 0;
            secondsBetweenBlocks = initialSecondsBetweenBlocks;
            secondsToFall = initialSecondsToFall;
            blockTiming.Interval = TimeSpan.FromSeconds(secondsBetweenBlocks);
            blockTiming.Start();
        }

        private void blockTimerDrop(object sender, EventArgs e)
        {
            if ((DateTime.Now.Subtract(lastAdjustmentTime).TotalSeconds >
                secondsBetweenAdjustments))
            {
                lastAdjustmentTime = DateTime.Now;

                secondsBetweenBlocks -= secondsBetweenBlocksReduction;
                secondsToFall -= secondsToFallReduction;

                // (Technically, you should check for 0 or negative values.
                // However, in practice these won't occur because the game will
                // always end first.)

                // Set the timer to drop the next Block at the appropriate time.
                blockTiming.Interval = TimeSpan.FromSeconds(secondsBetweenBlocks);

                // Update the status message.
                lblRate.Text = String.Format("A block is released at each {0} seconds.",
                    secondsBetweenBlocks);
                lblSpeed.Text = String.Format("Each block takes {0} seconds to fall.",
                    secondsToFall);
            }

            // Create the Block.
            Block blocks = new Block();
            blocks.IsFalling = true;

            // Position the Block.            
            Random random = new Random();
            blocks.SetValue(Canvas.LeftProperty,
                (double)(random.Next(0, (int)(canvasBackground.ActualWidth - 50))));
            blocks.SetValue(Canvas.TopProperty, -100.0);

            // Attach mouse click event (for defusing the Block).
            blocks.MouseLeftButtonDown += Block_MouseLeftButtonDown;

            // Create the animation for the falling Block.
            Storyboard storyboard = new Storyboard();
            DoubleAnimation fallAnimation = new DoubleAnimation();
            fallAnimation.To = canvasBackground.ActualHeight;
            fallAnimation.Duration = TimeSpan.FromSeconds(secondsToFall);

            Storyboard.SetTarget(fallAnimation, blocks);
            Storyboard.SetTargetProperty(fallAnimation, new PropertyPath("(Canvas.Top)"));
            storyboard.Children.Add(fallAnimation);
   
            // Add the Block to the Canvas.
            canvasBackground.Children.Add(blocks);

            // Add the storyboard to the tracking collection.            
            storyboards.Add(blocks, storyboard);

            // Configure and start the storyboard.
            storyboard.Duration = fallAnimation.Duration;
            storyboard.Completed += storyboard_Completed;
            storyboard.Begin();
        }

        private void Block_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get the Block.
            Block Block = (Block)sender;
            Block.IsFalling = false;

            // Get the Block's current position.
            Storyboard storyboard = storyboards[Block];
            double currentTop = Canvas.GetTop(Block);

            // Stop the Block from falling.
            storyboard.Stop();

            // Reuse the existing storyboard, but with new animations.
            // Send the Block on a new trajectory by animating Canvas.Top
            // and Canvas.Left.
            storyboard.Children.Clear();

            DoubleAnimation riseAnimation = new DoubleAnimation();
            riseAnimation.From = currentTop;
            riseAnimation.To = 0;
            riseAnimation.Duration = TimeSpan.FromSeconds(2);

            Storyboard.SetTarget(riseAnimation, Block);
            Storyboard.SetTargetProperty(riseAnimation, new PropertyPath("(Canvas.Top)"));
            storyboard.Children.Add(riseAnimation);

            DoubleAnimation slideAnimation = new DoubleAnimation();
            double currentLeft = Canvas.GetLeft(Block);
            // Throw the Block off the closest side.
            if (currentLeft < canvasBackground.ActualWidth / 2)
            {
                slideAnimation.To = -100;
            }
            else
            {
                slideAnimation.To = canvasBackground.ActualWidth + 100;
            }
            slideAnimation.Duration = TimeSpan.FromSeconds(1);
            Storyboard.SetTarget(slideAnimation, Block);
            Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(Canvas.Left)"));
            storyboard.Children.Add(slideAnimation);

            // Start the new animation.
            storyboard.Duration = slideAnimation.Duration;
            storyboard.Begin();
        }

        private void storyboard_Completed(object sender, EventArgs e)
        {
            ClockGroup clockGroup = (ClockGroup)sender;

            // Get the first animation in the storyboard, and use it to find the
            // Block that's being animated.
            DoubleAnimation completedAnimation = (DoubleAnimation)clockGroup.Children[0].Timeline;
            Block completedBlock = (Block)Storyboard.GetTarget(completedAnimation);

            // Determine if a Block fell or flew off the Canvas after being clicked.
            if (completedBlock.IsFalling)
            {
                droppedCount++;
            }
            else
            {
                savedCount++;
            }

            // Update the display.
            lblStatus.Text = String.Format("You have dropped {0} Blocks and saved {1}.",
                droppedCount, savedCount);

            // Check if it's game over.
            if (droppedCount >= maxDropped)
            {
                blockTiming.Stop();
                lblStatus.Text += "\r\n\r\nGame over.";

                // Find all the storyboards that are underway.
                foreach (KeyValuePair<Block, Storyboard> item in storyboards)
                {
                    Storyboard storyboard = item.Value;
                    Block Block = item.Key;

                    storyboard.Stop();
                    canvasBackground.Children.Remove(Block);
                }
                // Empty the tracking collection.
                storyboards.Clear();

                // Allow the user to start a new game.
                cmdStart.IsEnabled = true;
            }
            else
            {
                Storyboard storyboard = (Storyboard)clockGroup.Timeline;
                storyboard.Stop();

                storyboards.Remove(completedBlock);
                canvasBackground.Children.Remove(completedBlock);
            }
        }
    }
}
