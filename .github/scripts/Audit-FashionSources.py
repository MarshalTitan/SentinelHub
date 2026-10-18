"""Read public acquisition facts and cross-check Ornament row identities; review only."""
import datetime, hashlib, json, urllib.request
def get(url):
    req=urllib.request.Request(url,headers={"User-Agent":"SentinelHub-MetadataAudit/1.0 (https://github.com/MarshalTitan/SentinelHub)","Accept":"application/json"})
    with urllib.request.urlopen(req,timeout=90) as response: raw=response.read(4_000_001)
    if len(raw)>4_000_000: raise ValueError("Response too large")
    return json.loads(raw),hashlib.sha256(raw).hexdigest()
url="https://ffxivcollect.com/api/fashions?language=en"
body,digest=get(url)
if len(body["results"]) != body["count"] or not 20<=body["count"]<=200: raise ValueError("Incomplete fashions response")
rows=[]
for m in body["results"]:
    rows.append({"Id":m["id"],"EnglishName":m["name"],"Patch":m["patch"],"ItemId":m["item_id"],
        "Sources":[{"Type":s["type"],"Text":s["text"]} for s in m["sources"] if s["type"]!="Unknown" and s["text"]]})
snapshot={"Provider":"FFXIV Collect","Endpoint":url,"RetrievedUtc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "ResponseSha256":digest,"Permission":"https://ffxivcollect.com/ ; https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/","Accessories":sorted(rows,key=lambda r:r["Id"])}
print("FASHION_SNAPSHOT="+json.dumps(snapshot,ensure_ascii=True,separators=(",",":")))
game,gamehash=get("https://v2.xivapi.com/api/sheet/Ornament?fields=Singular,Icon,Model,Order&limit=500")
print("ORNAMENT_AUDIT="+json.dumps({"sha256":gamehash,"data":game},ensure_ascii=True,separators=(",",":")))
with open("fashion-source-review.json","w",encoding="utf-8") as f: json.dump(snapshot,f,ensure_ascii=True,indent=2)
