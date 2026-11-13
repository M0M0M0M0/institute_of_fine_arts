document.addEventListener("DOMContentLoaded", function () {
    const backToTopBtn = document.querySelector('.back-to-top-btn');
    
    if (backToTopBtn) {
        window.addEventListener('scroll', function () {
            if (window.pageYOffset > 500) {
                backToTopBtn.style.display = 'flex'; // Dùng flex để mũi tên nằm chính giữa tâm tròn
            } else {
                backToTopBtn.style.display = 'none';
            }
        });

        backToTopBtn.addEventListener('click', function () {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    // Tính năng Thả tim (Vote) ở trang danh sách
const voteButtons = document.querySelectorAll('.btn-vote');
voteButtons.forEach(btn => {
    btn.addEventListener('click', function (e) {
        e.preventDefault(); // Ngăn trình duyệt nhảy lên đầu trang

        let currentVotes = parseInt(this.getAttribute('data-votes'));
        const icon = this.querySelector('.vote-icon');
        const countSpan = this.querySelector('.vote-count');

        if (this.classList.contains('voted')) {
            // Hủy vote
            this.classList.remove('voted', 'border-danger', 'text-danger', 'bg-danger', 'bg-opacity-10');
            icon.classList.replace('bi-heart-fill', 'bi-heart');
            currentVotes--;
        } else {
            // Thực hiện vote
            this.classList.add('voted', 'border-danger', 'text-danger', 'bg-danger', 'bg-opacity-10');
            icon.classList.replace('bi-heart', 'bi-heart-fill');
            currentVotes++;
        }

        this.setAttribute('data-votes', currentVotes);
        countSpan.textContent = currentVotes;
    });
});

// Tính năng Thả tim (Vote) ở trang chi tiết (CommissionArtDetail)
const btnVoteDetail = document.querySelector('.btn-vote-detail');
if (btnVoteDetail) {
    btnVoteDetail.addEventListener('click', function () {
        const voteCountElem = document.getElementById('detailVoteCount');
        let currentVotes = parseInt(voteCountElem.textContent);
        const icon = this.querySelector('i');
        let isVoted = this.getAttribute('data-voted') === 'true';

        if (isVoted) {
            // Hủy vote
            this.classList.remove('voted', 'bg-danger', 'text-white');
            this.classList.add('btn-outline-danger');
            icon.classList.replace('bi-heart-fill', 'bi-heart');
            this.innerHTML = `<i class="bi bi-heart fs-5"></i> Vote for this piece`;
            this.setAttribute('data-voted', 'false');
            currentVotes--;
        } else {
            // Vote
            this.classList.remove('btn-outline-danger');
            this.classList.add('voted', 'bg-danger', 'text-white');
            icon.classList.replace('bi-heart', 'bi-heart-fill');
            this.innerHTML = `<i class="bi bi-heart-fill fs-5"></i> Voted!`;
            this.setAttribute('data-voted', 'true');
            currentVotes++;
        }
        
        voteCountElem.textContent = currentVotes;
    });
}
});
