"""Review-only permitted short acquisition facts; no images, descriptions, user statistics or current availability import."""
import datetime,hashlib,json,urllib.request

def get(url):
    request=urllib.request.Request(url,headers={"User-Agent":"SentinelHub-InspectionAudit/1.0 (https://github.com/MarshalTitan/SentinelHub)","Accept":"application/json"})
    with urllib.request.urlopen(request,timeout=45) as response: raw=response.read(4_000_001)
    if len(raw)>4_000_000: raise ValueError("Response exceeds audit bound")
    return json.loads(raw),hashlib.sha256(raw).hexdigest()

for endpoint,kind,sheet,name in [("bardings","Barding","BuddyEquip","Name"),("emotes","Emote","Emote","Name")]:
    url="https://ffxivcollect.com/api/"+endpoint+"?language=en"
    body,digest=get(url)
    if len(body["results"])!=body["count"] or not 20<=body["count"]<=1000: raise ValueError("Incomplete result set")
    game,game_digest=get("https://v2.xivapi.com/api/sheet/"+sheet+"?fields="+name+"&limit=1000")
    identities={r["row_id"]:r["fields"][name] for r in game["rows"]}
    rows=[]
    for m in body["results"]:
        if not isinstance(m["id"],int) or not 0<m["id"]<=65535 or not isinstance(m["name"],str) or len(m["name"])>200: raise ValueError("Invalid identity")
        sources=[{"Type":s["type"],"Text":s["text"]} for s in m["sources"] if s["type"]!="Unknown" and s["text"]]
        if any(len(s["Text"])>1000 or any(ord(c)<32 for c in s["Text"]) for s in sources): raise ValueError("Invalid source text")
        rows.append({"Id":m["id"],"EnglishName":m["name"],"Patch":m.get("patch"),"Sources":sources})
    if len({r["Id"] for r in rows})!=len(rows): raise ValueError("Duplicate identity")
    matched=[r for r in rows if identities.get(r["Id"],"").casefold()==r["EnglishName"].casefold()]
    mismatched=[r["Id"] for r in rows if r not in matched]
    snapshot={"Kind":kind,"Provider":"FFXIV Collect","Endpoint":url,"RetrievedUtc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "ResponseSha256":digest,"Permission":"https://ffxivcollect.com/ ; https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/",
        "Rows":rows}
    filename="inspection-"+endpoint+"-review.json"
    with open(filename,"w",encoding="utf-8") as output: json.dump(snapshot,output,ensure_ascii=True,indent=2)
    print(json.dumps({"kind":kind,"records":len(rows),"with_sources":sum(bool(r["Sources"]) for r in rows),"game_identity_matches":len(matched),"unmatched_ids":mismatched,"game_version":game["version"],"game_sha256":game_digest,"source_sha256":digest}))
    # Candidate short facts are visible for explicit human/architect review. No raw game table is logged or bundled.
    print("SENTINEL_REVIEW_"+kind+"="+json.dumps(snapshot,ensure_ascii=True,separators=(",",":")))
