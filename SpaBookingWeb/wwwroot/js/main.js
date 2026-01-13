//document.addEventListener('DOMContentLoaded', () => {
//    // Lấy tên file hiện tại
//    const currentPath = window.location.pathname.split("/").pop();

//    // Tìm thẻ a trong sidebar có href trùng với file hiện tại
//    const menuItems = document.querySelectorAll('aside a');

//    menuItems.forEach(item => {
//        const href = item.getAttribute('href');
//        if (href === currentPath || (currentPath === '' && href === 'index.html')) {
//            // Thêm class active (background primary nhạt + chữ primary)
//            item.classList.add('bg-primary/20', 'text-primary');
//            item.classList.remove('text-gray-700', 'dark:text-gray-300', 'text-[#618389]');

//            // Đổi icon sang dạng fill (nếu dùng material symbols)
//            const icon = item.querySelector('.material-symbols-outlined');
//            if(icon) icon.style.fontVariationSettings = "'FILL' 1";
//        } else {
//            // Đảm bảo các item khác ở trạng thái thường
//            item.classList.remove('bg-primary/20', 'text-primary');
//            item.classList.add('text-gray-700', 'dark:text-gray-300');
//        }
//    });
//});

document.addEventListener('DOMContentLoaded', () => {
    // 1. Lấy đường dẫn hiện tại trên trình duyệt và chuyển về chữ thường
    // Ví dụ: /Technician/Home/Index
    let currentPath = window.location.pathname.toLowerCase();

    // Xử lý chuẩn hóa: Xóa dấu / ở cuối nếu có để so sánh chính xác hơn
    if (currentPath.endsWith('/') && currentPath.length > 1) {
        currentPath = currentPath.slice(0, -1);
    }

    // 2. Lấy tất cả các thẻ a trong sidebar
    const menuItems = document.querySelectorAll('aside a');

    menuItems.forEach(item => {
        // 3. Lấy đường dẫn mục tiêu: Ưu tiên 'data-route' (bạn đã thêm), nếu không có thì lấy 'href'
        let targetRoute = item.getAttribute('data-route') || item.getAttribute('href');

        if (targetRoute) {
            targetRoute = targetRoute.toLowerCase();
            // Chuẩn hóa targetRoute tương tự
            if (targetRoute.endsWith('/') && targetRoute.length > 1) {
                targetRoute = targetRoute.slice(0, -1);
            }

            // 4. Logic so sánh:
            // - Trùng khớp hoàn toàn (Exact match)
            // - Hoặc là trang con (Ví dụ vào "Tạo lịch hẹn" thì menu "Lịch hẹn" vẫn sáng nếu cấu trúc url cha con)
            // - Fix lỗi trang chủ: Xử lý trường hợp /Technician tương đương /Technician/Home/Index

            const isMatch =
                currentPath === targetRoute ||
                (currentPath === '/technician/home/index' && targetRoute === '/technician') || // Fix riêng cho dashboard
                (currentPath.startsWith(targetRoute) && targetRoute !== '/' && targetRoute !== '/technician');

            // 5. Áp dụng Style
            if (isMatch) {
                // Active state
                item.classList.add('bg-primary/20', 'text-primary');
                item.classList.remove('text-gray-700', 'dark:text-gray-300');

                const icon = item.querySelector('.material-symbols-outlined');
                if (icon) icon.style.fontVariationSettings = "'FILL' 1";
            } else {
                // Inactive state
                item.classList.remove('bg-primary/20', 'text-primary');
                item.classList.add('text-gray-700', 'dark:text-gray-300');

                const icon = item.querySelector('.material-symbols-outlined');
                if (icon) icon.style.fontVariationSettings = "'FILL' 0";
            }
        }
    });
});

