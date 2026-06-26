using StudyConnect.Models;

namespace StudyConnect.ViewModels;

public class ClubCommunityViewModel
{
    public int? CurrentSinhVienId { get; set; }

    public List<CauLacBo> Clubs { get; set; } = [];

    public List<ThanhVienClb> MyMemberships { get; set; } = [];

    public List<HoatDongClb> Activities { get; set; } = [];

    public List<TaiLieuClb> Documents { get; set; } = [];

    public List<DotDeCuPhoChuNhiem> Elections { get; set; } = [];

    public Dictionary<int, int> MemberCounts { get; set; } = [];

    public Dictionary<int, int> CandidateVoteCounts { get; set; } = [];

    public Dictionary<int, int> MyVotesByElection { get; set; } = [];
}
