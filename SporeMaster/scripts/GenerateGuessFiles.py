# Use a variable order markov model (PPM-C) to generate a large amount of gibberish
#   statistically similar to Spore filenames.  Divide it into left and right "halves"
#   and build guess_left.txt and guess_right.txt.  The names "tried" by the SporeMaster
#   hash guesser (with no prefix, suffix, or contains string) are all combinations of a
#   left half from guess_left.txt and a right half from guess_right.txt.

import os

if 1:
    source = []
    dir = "..\\SporeMaster\\bin\\release\\"

    for file in [dir + "reg_property.txt",
                 dir + "reg_file.txt",
                 ]:
        for line in open(file,"rt"):
            line = line.split("\t",1)[0].lower()
            source.append(line)

    for file in os.listdir( dir + "spore.unpacked\\locale~"):
        for line in open( dir + "spore.unpacked\\locale~\\" + file, "rt"):
            if line.startswith("0x"):
                words = line.lower().strip().split(" ",1)[1:]
                for i in range( len(words)//5 ):
                    source.append( ''.join( words[i*5:i*5+5] ) )

    source = '\n'.join(source)
    open("source2.txt","wb").write(source)

M = 5
esc = 0
line_division = 0.4

for side in ("left", "right"):
    src = open("source2.txt","rb").readlines()

    model = [ {} for i in range(M) ]

    # strip and uniquify
    src = list(set(s.strip() for s in src))

    if side == "left":
        # take left half of line!
        src = list(s[:int(len(s)*line_division)] for s in src)
    elif side == "right":
        # take and reverse right half of line!
        src = list(s[int(len(s)*line_division):][::-1] for s in src)

    for line in src:
        line = "\n" + line + "\n"
        for i in range(len(line)):
            for m in range(M):
                if i >= m:
                    d = model[m].setdefault(line[i-m:i],{})
                    d[line[i]] = d.get(line[i],0) + 1

    for m in model:
        for k in m:
            m[k][esc] = len(m[k])  # PPM "Method C"
            total = sum(m[k].values())
            items = m[k].items()
            items = list( (b,a) for (a,b) in items )
            items.sort()
            items = list( (b,a) for (a,b) in items )
            
            m[k] = (total, ) + tuple( items[::-1] )

    model_pref = range(M)[::-1]

    def decode( context, p ):
        for m in model_pref:
            if m > len(context): continue
            cx = context[len(context)-m:]
            if cx not in model[m]: continue
            node = model[m][cx]
    ##        print repr(cx), node
            total = node[0]
            children = node[1:]
            px = p * total
            for alt,pa in children:
                if px < pa:
                    p = px / pa
                    if alt != esc:
                        return alt, p
                    # Escape to smaller context
                    break
                else:
                    px -= pa
            else:
                raise "Do not want."
        return chr( int(p * 256) ), p - int(p * 256)

    def gen( context, p ):
        cx2 = context
        while (context==cx2 or not context.endswith("\n")) and len(context)<50:
            c,p = decode(context,p)
            context += c
        return context

    def top(i):
        f = 0.0
        q = 1.0
        while i:
            q *= 0.5
            if i&1:
                f += q
            i >>= 1
        return f

    all = set()

    if side == "left":
        outf = open("guess_left.txt","wb")
        i = 0
        while len(all) < 250000:
            d = gen("\n",top(i)).strip()
            i += 1
            if d not in all:
                all.add(d)
                outf.write( d + "\r\n" )
        outf.close()
        print i
    elif side == "right":
        outf = open("guess_right.txt","wb")
        i = 0
        while len(all) < 1000000:
            d = gen("\n",top(i)).strip()[::-1]
            i += 1
            if d not in all:
                all.add(d)
                outf.write( d + "\r\n" )
        outf.close()
        print i
