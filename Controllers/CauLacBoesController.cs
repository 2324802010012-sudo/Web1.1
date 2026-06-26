
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Models;
using StudyConnect.Data;

public class CauLacBoesController : Controller
{
    private readonly AppDbContext _context;

    public CauLacBoesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: CAULACBOS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.CauLacBos.ToListAsync());
    }

    // GET: CAULACBOS/Details/5
    public async Task<IActionResult> Details(int? maclb)
    {
        if (maclb == null)
        {
            return NotFound();
        }

        var caulacbo = await _context.CauLacBos
            .FirstOrDefaultAsync(m => m.MaClb == maclb);
        if (caulacbo == null)
        {
            return NotFound();
        }

        return View(caulacbo);
    }

    // GET: CAULACBOS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: CAULACBOS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MaClb,TenClb,MoTa,NgayThanhLap,TrangThai,DotDeCuPhoChuNhiems,HoatDongClbs,TaiLieuClbs,ThanhVienClbs")] CauLacBo caulacbo)
    {
        if (ModelState.IsValid)
        {
            _context.Add(caulacbo);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(caulacbo);
    }

    // GET: CAULACBOS/Edit/5
    public async Task<IActionResult> Edit(int? maclb)
    {
        if (maclb == null)
        {
            return NotFound();
        }

        var caulacbo = await _context.CauLacBos.FindAsync(maclb);
        if (caulacbo == null)
        {
            return NotFound();
        }
        return View(caulacbo);
    }

    // POST: CAULACBOS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? maclb, [Bind("MaClb,TenClb,MoTa,NgayThanhLap,TrangThai,DotDeCuPhoChuNhiems,HoatDongClbs,TaiLieuClbs,ThanhVienClbs")] CauLacBo caulacbo)
    {
        if (maclb != caulacbo.MaClb)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(caulacbo);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CauLacBoExists(caulacbo.MaClb))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(caulacbo);
    }

    // GET: CAULACBOS/Delete/5
    public async Task<IActionResult> Delete(int? maclb)
    {
        if (maclb == null)
        {
            return NotFound();
        }

        var caulacbo = await _context.CauLacBos
            .FirstOrDefaultAsync(m => m.MaClb == maclb);
        if (caulacbo == null)
        {
            return NotFound();
        }

        return View(caulacbo);
    }

    // POST: CAULACBOS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? maclb)
    {
        var caulacbo = await _context.CauLacBos.FindAsync(maclb);
        if (caulacbo != null)
        {
            _context.CauLacBos.Remove(caulacbo);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool CauLacBoExists(int? maclb)
    {
        return _context.CauLacBos.Any(e => e.MaClb == maclb);
    }
}
