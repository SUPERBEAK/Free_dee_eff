using System;
using Freedeeeff.ViewModels;
using Xunit;

namespace Freedeeeff.Tests
{
    public class ViewModelZoomAndNavigationTests
    {
        [Fact]
        public void PageCounter_DefaultsToZero_AndComputesOneBasedPageNumber()
        {
            var vm = new MainViewModel();
            Assert.Equal(0, vm.TotalPages);
            Assert.Equal(0, vm.CurrentPageNumber);

            vm.TotalPages = 5;
            vm.CurrentPageIndex = 0;
            Assert.Equal(1, vm.CurrentPageNumber);

            vm.CurrentPageIndex = 3;
            Assert.Equal(4, vm.CurrentPageNumber);
        }

        [Fact]
        public void PageNavigation_NextAndPreviousCommands_ClampCorrectly()
        {
            var vm = new MainViewModel();
            vm.TotalPages = 3;
            vm.Pages.Add(new PdfPageViewModel(0, 612, 792));
            vm.Pages.Add(new PdfPageViewModel(1, 612, 792));
            vm.Pages.Add(new PdfPageViewModel(2, 612, 792));

            vm.CurrentPageIndex = 0;
            Assert.Equal(1, vm.CurrentPageNumber);

            // Previous on page 0 does not go negative
            vm.PreviousPage();
            Assert.Equal(0, vm.CurrentPageIndex);
            Assert.Equal(1, vm.CurrentPageNumber);

            // Next page increments
            vm.NextPage();
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal(2, vm.CurrentPageNumber);

            vm.NextPage();
            Assert.Equal(2, vm.CurrentPageIndex);
            Assert.Equal(3, vm.CurrentPageNumber);

            // Next page does not exceed bounds
            vm.NextPage();
            Assert.Equal(2, vm.CurrentPageIndex);
            Assert.Equal(3, vm.CurrentPageNumber);
        }

        [Fact]
        public void ZoomCommands_AdjustZoomLevelAndDisableFitWidthMode()
        {
            var vm = new MainViewModel();
            vm.SetZoomDirect(1.0);
            vm.IsFitWidthMode = true;

            vm.ZoomIn();
            Assert.True(vm.ZoomLevel > 1.0);
            Assert.False(vm.IsFitWidthMode);

            vm.ZoomOut();
            Assert.False(vm.IsFitWidthMode);

            vm.ZoomReset();
            Assert.Equal(1.0, vm.ZoomLevel);
            Assert.False(vm.IsFitWidthMode);
        }

        [Fact]
        public void ZoomFitWidth_InvokesRequestActionAndEnablesFitWidthMode()
        {
            var vm = new MainViewModel();
            vm.IsFitWidthMode = false;

            bool actionInvoked = false;
            vm.RequestFitWidthAction = () =>
            {
                actionInvoked = true;
                vm.SetZoomDirect(1.65);
            };

            vm.ZoomFitWidth();
            Assert.True(vm.IsFitWidthMode);
            Assert.True(actionInvoked);
            Assert.Equal(1.65, vm.ZoomLevel);
        }
    }
}
